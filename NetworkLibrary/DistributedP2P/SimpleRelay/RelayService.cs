using NetworkLibrary.Components.Crypto.DigitalSignature;
using NetworkLibrary.DistributedP2P.Components;
using NetworkLibrary.DistributedP2P.Server.StateManagement;
using NetworkLibrary.TCP.Base;
using NetworkLibrary.UDP;
using NetworkLibrary.Utils;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkLibrary.DistributedP2P.SimpleRelay
{
    internal class PipeToken
    {
        public Guid Token;
        public DateTime Expiration;

        internal bool IsExpired()
        {
            DateTime now = DateTime.UtcNow;
            if (now > Expiration)
                return true;

            return false;
        }
    }
    internal class TcpTokenStorage
    {
        internal byte[] Token = new byte[PipeData.TokenLength];
        int tokenOffset = 0;

        internal int StoreTokenFragment(byte[] tokenFragment, int offset, int count)
        {
            if (count > PipeData.TokenLength - tokenOffset)
            {
                return -1;
            }
            else
            {
                Buffer.BlockCopy(tokenFragment, offset, Token, tokenOffset, count);
                tokenOffset += count;
                if (tokenOffset == PipeData.TokenLength)
                {
                    return 1;
                }
                return 0;
            }
        }
    }

    internal class RelayService : IDisposable
    {
        int tokenLifetimeMs = 200000;

        internal AsyncTcpServer TcpServer;
        internal AsyncUdpServer UdpServer;

        internal ConcurrentDictionary<Guid, Guid> pipeMapTcp = new ConcurrentDictionary<Guid, Guid>();
        internal ConcurrentDictionary<IPEndPoint, IPEndPoint> pipeMapUdp = new ConcurrentDictionary<IPEndPoint, IPEndPoint>();

        private ConcurrentDictionary<Guid, PipeState<Guid>> activeTcpPipeStates = new ConcurrentDictionary<Guid, PipeState<Guid>>();
        private ConcurrentDictionary<Guid, PipeState<IPEndPoint>> activeUdpPipeStates = new ConcurrentDictionary<Guid, PipeState<IPEndPoint>>();

        // Guid will be the id of coming peer, determined by relay.
        //Room state will hold room id.
        private ConcurrentDictionary<Guid, PeerRoomState> activeRoomStates = new ConcurrentDictionary<Guid, PeerRoomState>();
        private ConcurrentDictionary<Guid, Room> activeRooms = new ConcurrentDictionary<Guid, Room>();

        private ConcurrentDictionary<Guid, Room> roomMapTcp = new ConcurrentDictionary<Guid, Room>();
        private ConcurrentDictionary<IPEndPoint, Room> roomMapUdp = new ConcurrentDictionary<IPEndPoint, Room>();

        private readonly ConcurrentDictionary<Guid, TcpTokenStorage> tokenStorage = new ConcurrentDictionary<Guid, TcpTokenStorage>();

        byte[] cryptoKey = new byte[32];
        PrivateKeySign signer;
        readonly object tokenMtex = new object();

        public RelayService(int TcpPort, int UdpPort)
        {
            var rng = RandomNumberGenerator.Create();
            rng.GetBytes(cryptoKey, 0, cryptoKey.Length);

            TcpServer = new AsyncTcpServer(TcpPort);
            TcpServer.GatherConfig = ScatterGatherConfig.UseBuffer;
            UdpServer = new AsyncUdpServer(UdpPort);
            UdpServer.ClientDisconnected += HandleUdpPipeDisconnect;

            TcpServer.OnClientAccepted += TcpClientAccepted;
            TcpServer.OnClientDisconnected += HandleTcpPipeDisconnect;

            TcpServer.OnBytesReceived += HandleTcpBytes;
            UdpServer.OnBytesRecieved += HandleUdpBytes;
            signer = new PrivateKeySign(cryptoKey);


            UdpServer.StartServer();
            TcpServer.StartServer();
            //print();

        }

        //private async Task print()
        //{
        //    while (true)
        //    {
        //        await Task.Delay(1000);
        //        long s = Interlocked.Exchange(ref sent, 0);
        //        Console.WriteLine(s.ToString("N1"));
        //    }
        //}

        private void TcpClientAccepted(Guid guid)
        {
            tokenStorage.TryAdd(guid, new TcpTokenStorage());
            TimerService.RegisterTimer(guid, tokenLifetimeMs, () => HandleTcpClientTimeout(guid));

        }

        private void HandleTcpClientTimeout(Guid guid)
        {
            if (!pipeMapTcp.ContainsKey(guid))
            {
                tokenStorage.TryRemove(guid, out _);
                TcpServer.CloseSession(guid);
                // ddos here
            }
        }

        private void CancelTimeout(Guid guid)
        {
            TimerService.CancelTimeout(guid);
            tokenStorage.TryRemove(guid, out _);
        }



        private void HandleTcpBytes(Guid clientId, byte[] bytes, int offset, int count)
        {
            if (!RouteTcp(clientId, bytes, offset, count))
            {
                ManagePipeToken(clientId, bytes, offset, count);
            }
        }

        private void HandleUdpBytes(IPEndPoint endpoint, byte[] bytes, int offset, int count)
        {
            if (!RouteUdp(endpoint, bytes, offset, count))
            {
                ManagePipeToken(endpoint, bytes, offset, count);
            }
        }

        long sent;
        private bool RouteTcp(Guid guid, byte[] bytes, int offset, int count)
        {
            // we should look for room stuff here
            if (pipeMapTcp.TryGetValue(guid, out var to))
            {
               // Interlocked.Add(ref sent, count);
                TcpServer.SendBytesToClientDirect(to, bytes, offset, count);
                return true;
            }
            else if (roomMapTcp.TryGetValue(guid, out var room))
            {
                room.HandleMessage(guid, bytes, offset, count);
                return true;
            }
            return false;
        }

        private bool RouteUdp(IPEndPoint endpoint, byte[] bytes, int offset, int count)
        {
            if (pipeMapUdp.TryGetValue(endpoint, out var to))
            {
                UdpServer.SendBytesToClient(to, bytes, offset, count);
                return true;
            }
            else if (roomMapUdp.TryGetValue(endpoint, out var room))
            {
                room.HandleMessage(endpoint, bytes, offset, count);
                return true;
            }
            return false;
        }


        //tcp
        private void ManagePipeToken(Guid ephemeralId, byte[] bytes, int offset, int count)
        {
            // state object etc
            lock (tokenMtex)
            {
                tokenStorage.TryGetValue(ephemeralId, out var storage);

                int result = storage.StoreTokenFragment(bytes, offset, count);

                if (result == 0)//incomplete
                    return;

                if (result == -1)// nonsense
                {
                    RemoveTcpClient(ephemeralId);
                    return;
                }

                if (result == 1)// tokenComplete
                {
                    PipeToken token = DeserializePipeToken(storage.Token, 0);

                    if (activeTcpPipeStates.TryGetValue(token.Token, out var pipeState))
                    {
                        if (VerifyToken(storage.Token, token.Expiration))
                        {
                            Console.WriteLine("TokenVerified");
                            pipeState.RegisterClient(ephemeralId);

                            if (pipeState.IsComplete())
                            {
                                activeTcpPipeStates.TryRemove(token.Token, out _);
                                TcpPipeCreated(pipeState.Clients[0], pipeState.Clients[1]);
                            }

                            TcpServer.SendBytesToClientDirect(ephemeralId, new byte[1] { 0x01 },0,1);
                        }
                        else
                        {
                            Console.WriteLine("Token Rejected");
                            RemoveTcpClient(ephemeralId);
                        }
                    }
                    else if (activeRoomStates.TryRemove(token.Token, out var roomState))//token is peerId
                    {
                        if (VerifyToken(storage.Token, token.Expiration))
                        {
                            if (roomState.Verify(token, ephemeralId))
                            {
                                if (activeRooms.TryGetValue(roomState.RoomId, out Room room))
                                {
                                    room.Add(token.Token, roomState);
                                    TcpServer.SendBytesToClientDirect(ephemeralId, new byte[1] { 0x01 }, 0, 1);
                                }
                            }
                        }
                        else
                        {
                            RemoveTcpClient(ephemeralId);
                        }

                    }
                    else
                    {
                        RemoveTcpClient(ephemeralId);
                    }
                }
                else
                {
                    RemoveTcpClient(ephemeralId);
                }
            }
        }

        //udp
        private void ManagePipeToken(IPEndPoint clientEp, byte[] bytes, int offset, int count)
        {
            lock (tokenMtex)
            {
                byte[] tokenBytes = ByteCopy.ToArray(bytes, offset, count);

                if (tokenBytes.Length != PipeData.TokenLength)
                {
                    return;
                }

                PipeToken token = DeserializePipeToken(tokenBytes, 0);


                if (activeUdpPipeStates.TryGetValue(token.Token, out var pipeState))
                {
                    if (VerifyToken(tokenBytes, token.Expiration))
                    {
                        pipeState.RegisterClient(clientEp);

                        if (pipeState.IsComplete())
                        {
                            activeUdpPipeStates.TryRemove(token.Token, out _);
                            UdpPipeCreated(pipeState.Clients[0], pipeState.Clients[1]);
                        }

                        UdpServer.SendBytesToClient(clientEp, new byte[1] { 0x01 }, 0, 1);
                    }
                    else
                    {
                        // ddos here.
                        UdpServer.RemoveClient(clientEp);
                    }
                }
                else if (activeRoomStates.TryRemove(token.Token, out var roomState))//token is peerId
                {
                    if (VerifyToken(tokenBytes, token.Expiration))
                    {
                        if (roomState.Verify(token, clientEp))
                        {
                            if (activeRooms.TryGetValue(roomState.RoomId, out Room room))
                            {
                                room.Add(token.Token, roomState);
                                UdpServer.SendBytesToClient(clientEp, new byte[1] { 0x01 }, 0, 1);

                            }
                        }
                    }
                    else
                    {
                        UdpServer.RemoveClient(clientEp);
                    }
                }

                else
                {
                    UdpServer.RemoveClient(clientEp);
                }
            }

        }

        private bool VerifyToken(byte[] Token, DateTime expiration)
        {
            var calculatedSignature = signer.Sign(Token, 0, 24);
            if (SignatureMatch(calculatedSignature, Token))
            {
                if (DateTime.UtcNow <= expiration)
                    return true;
                return false;
            }
            else
            {
                Console.WriteLine("Signature did not match");
                return false;
            }
        }

        private bool SignatureMatch(byte[] localSignature, byte[] incomingSignature)
        {
            for (int i = 0; i < 32; i++)
            {
                if (localSignature[i] != incomingSignature[24 + i])
                    return false;
            }
            return true;
        }


        private PipeToken DeserializePipeToken(byte[] bytes, int offset)
        {
            var token = PrimitiveEncoder.ReadGuid(bytes, ref offset);//16
            var expiration = DateTime.FromBinary(PrimitiveEncoder.ReadFixedInt64(bytes, ref offset));//8

            return new PipeToken() { Token = token, Expiration = expiration };
        }

        private void RemoveTcpClient(Guid guid)
        {
            TcpServer.CloseSession(guid);
        }

        private void TcpPipeCreated(Guid from, Guid to)
        {
            pipeMapTcp.TryAdd(from, to);
            pipeMapTcp.TryAdd(to, from);

            CancelTimeout(from);
            CancelTimeout(to);
        }

        private void UdpPipeCreated(IPEndPoint from, IPEndPoint to)
        {
            pipeMapUdp.TryAdd(from, to);
            pipeMapUdp.TryAdd(to, from);
        }

        private void HandleUdpPipeDisconnect(IPEndPoint from)
        {
            if (pipeMapUdp.TryRemove(from, out var to))
            {
                pipeMapUdp.TryRemove(to, out _);
            }
            else if(roomMapUdp.TryRemove(from, out Room room))
            {
                room.HandleDisconnect(from);
            }
        }

        private void HandleTcpPipeDisconnect(Guid from)
        {
            if (pipeMapTcp.TryRemove(from, out Guid to))
            {
                TcpServer.CloseSession(to);
                pipeMapTcp.TryRemove(to, out _);
            }
            else if (roomMapTcp.TryRemove(from, out Room room))
            {
                room.HandleDisconnect(from);
            }

            tokenStorage.TryRemove(from, out _);
        }


        private void RegisterTcpToken(PipeToken pipeData)
        {
            activeTcpPipeStates.TryAdd(pipeData.Token, new PipeState<Guid>(pipeData));
            TimerService.RegisterTimer(pipeData.Token, tokenLifetimeMs, () => activeTcpPipeStates.TryRemove(pipeData.Token, out _));
        }

        private void RegisterUdpToken(PipeToken pipeData)
        {
            activeUdpPipeStates.TryAdd(pipeData.Token, new PipeState<IPEndPoint>(pipeData));
            TimerService.RegisterTimer(pipeData.Token, tokenLifetimeMs, () => activeUdpPipeStates.TryRemove(pipeData.Token, out _));

        }
        private void Createtoken(Guid tokenId, out byte[] data, out PipeToken pipeData)
        {
            data = new byte[PipeData.TokenLength];
            int offset = 0;
            pipeData = new PipeToken();
            pipeData.Token = tokenId;
            pipeData.Expiration = DateTime.UtcNow.AddMilliseconds(tokenLifetimeMs);

            PrimitiveEncoder.WriteGuid(data, ref offset, pipeData.Token);//16

            long Time = pipeData.Expiration.ToBinary();
            PrimitiveEncoder.WriteFixedInt64(data, ref offset, Time); //8

            byte[] signature = signer.Sign(data, 0, 24);
            Buffer.BlockCopy(signature, 0, data, offset, 32);//32
        }




        // for direct p2p
        public byte[] GetPipeToken(bool tcp)
        {
            byte[] data;
            PipeToken pipeData;
            Createtoken(Guid.NewGuid(), out data, out pipeData);

            if (tcp)
                RegisterTcpToken(pipeData);
            else
                RegisterUdpToken(pipeData);

            return data;
        }

        //for broadcast
        public bool CreateRoom(Guid roomId, RoomProtocol protocol)
        {
            Room room = new Room(roomId, protocol);
            room.PeerRegistered += HandleRoomPeerRegistered;
            room.PeerLeft += HandleRoomPeerLeft;
            room.SendMessage += RouteRoomMessage;
            room.RoomDestroyed += () => activeRooms.TryRemove(room.roomId, out _);

            bool good = activeRooms.TryAdd(roomId, room);
            if (good)
            {
                return true;
            }
            else
            {
                room.Clear();
                return false;
            }
        }

        // get token for spesific peer, who wants to join a room
        public byte[] GetRoomToken(Guid roomId, Guid peerId)
        {
            if (activeRooms.TryGetValue(roomId, out var room))
            {
                PeerRoomState state = new PeerRoomState();
                state.RoomId = roomId;
                state.ExpectedClient = peerId;

                Createtoken(peerId, out byte[] data, out PipeToken token);

                if (activeRoomStates.TryAdd(peerId, state))
                    return data;

                return null;
            }
            return null;
        }

        public void RemovePeerFromRoom(Guid roomId, Guid peerId)
        {
            if (activeRooms.TryGetValue(roomId, out var room))
            {
                room.RemovePeer(peerId);

            }
        }

        private void RouteRoomMessage(PeerRoomState to, byte[] b, int o, int c)
        {
            if (to.isTcp)
            {
                TcpServer.SendBytesToClientDirect(to.EphemeralId, b, o, c);
            }
            else
            {
                UdpServer.SendBytesToClient(to.AssociatedEndpoint, b, o, c);
            }
        }

        private void HandleRoomPeerLeft(PeerRoomState state)
        {
            if (state.isTcp)
            {
                roomMapTcp.TryRemove(state.EphemeralId, out _);
            }
            else
            {
                roomMapUdp.TryRemove(state.AssociatedEndpoint, out _);
            }
        }

        private void HandleRoomPeerRegistered(PeerRoomState state)
        {
            throw new NotImplementedException();
        }
        public void Dispose()
        {
            TcpServer.ShutdownServer();
            UdpServer.Dispose();
        }
    }


}
