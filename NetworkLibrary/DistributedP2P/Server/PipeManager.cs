using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Text;
using NetworkLibrary.Components;
using NetworkLibrary.Components.Crypto.DigitalSignature;
using NetworkLibrary.MessageProtocol;
using NetworkLibrary.TCP.Base;
using NetworkLibrary.UDP;
using NetworkLibrary.P2P.Components.HolePunch;
using NetworkLibrary.Utils;
using System.IO;
using NetworkLibrary.DistributedP2P.Components;
using NetworkLibrary.DistributedP2P.Server.StateManagement;
using System.Threading.Tasks;
using System.Threading;
using System.Security.Cryptography;
namespace NetworkLibrary.DistributedP2P.Server
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

    internal class PipeManager
    {
        int tokenLifetimeMs = 20000;

        internal AsyncTcpServer TcpServer;
        internal AsyncUdpServer UdpServer;

        internal ConcurrentDictionary<Guid, Guid> pipeMapTcp = new ConcurrentDictionary<Guid, Guid>();
        internal ConcurrentDictionary<IPEndPoint, IPEndPoint> pipeMapUdp = new ConcurrentDictionary<IPEndPoint, IPEndPoint>();

        private ConcurrentDictionary<Guid, PipeState<Guid>> activeTcpPipeStates = new ConcurrentDictionary<Guid, PipeState<Guid>>();
        private ConcurrentDictionary<Guid, PipeState<IPEndPoint>> activeUdpPipeStates = new ConcurrentDictionary<Guid, PipeState<IPEndPoint>>();

        private readonly ConcurrentDictionary<Guid, TcpTokenStorage> tokenStorage = new ConcurrentDictionary<Guid, TcpTokenStorage>();

        byte[] cryptoKey;
        PrivateKeySign signer;
        readonly object tokenMtex = new object();

        public PipeManager(AsyncTcpServer tcpServer, AsyncUdpServer udpServer, byte[] pipeKey)
        {
            cryptoKey = pipeKey;

            TcpServer = tcpServer;
            UdpServer = udpServer;

            TcpServer.OnClientAccepted += TcpClientAccepted;
            TcpServer.OnClientDisconnected += HandleTcpPipeDisconnect;

            TcpServer.OnBytesReceived += HandleTcpBytes;
            UdpServer.OnBytesRecieved += HandleUdpBytes;
            signer = new PrivateKeySign(pipeKey);

        }

     
        private void TcpClientAccepted(Guid guid)
        {
            tokenStorage.TryAdd(guid, new TcpTokenStorage());
            TimerService.RegisterTimer(guid, tokenLifetimeMs, ()=>HandleTcpClientTimeout(guid));
           
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


        private bool RouteTcp(Guid guid, byte[] bytes, int offset, int count)
        {
            // we should look for room stuff here
            if (pipeMapTcp.TryGetValue(guid, out var to))
            {
                TcpServer.SendBytesToClient(to, bytes, offset, count);
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
            return false;
        }


        //tcp
        private void ManagePipeToken(Guid guid, byte[] bytes, int offset, int count)
        {
            // state object etc
            lock (tokenMtex)
            {
                tokenStorage.TryGetValue(guid, out var storage);

                int result = storage.StoreTokenFragment(bytes, offset, count);

                if (result == 0)//incomplete
                    return;

                if (result == -1)// nonsense
                {
                    RemoveTcpClient(guid);
                    return;
                }

                if (result == 1)// tokenComplete
                {
                    PipeToken token = DeserializePipeToken(storage.Token, 0);

                    if (activeTcpPipeStates.TryGetValue(token.Token, out var pipeState)) 
                    {
                        if (VerifyToken(storage.Token, token.Expiration))
                        {
                            pipeState.RegisterClient(guid);

                            if (pipeState.IsComplete())
                            {
                                activeTcpPipeStates.TryRemove(token.Token, out _);
                                TcpPipeCreated(pipeState.Clients[0], pipeState.Clients[1]);
                            }

                            TcpServer.SendBytesToClient(guid, new byte[1] { 0x01 });
                        }
                        else
                        {
                            RemoveTcpClient(guid);
                        }
                    }
                    else
                    {
                        RemoveTcpClient(guid);
                    }
                }
                else
                {
                    RemoveTcpClient(guid);
                }
            }
        }

        //udp
        private void ManagePipeToken(IPEndPoint clientEp, byte[] bytes, int offset, int count)
        {
            lock (tokenMtex)
            {
                byte[] tokenBytes = ByteCopy.ToArray(bytes, offset, count);

                if(tokenBytes.Length != PipeData.TokenLength)
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
               if (DateTime.UtcNow < expiration)
                    return true;
                return false;
            }
            else
            {
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

        internal void TcpPipeCreated(Guid from, Guid to)
        {
            pipeMapTcp.TryAdd(from, to);
            pipeMapTcp.TryAdd(to, from);

            CancelTimeout(from);
            CancelTimeout(to);
        }

        internal void UdpPipeCreated(IPEndPoint from, IPEndPoint to)
        {
            pipeMapUdp.TryAdd(from, to);
            pipeMapUdp.TryAdd(to, from);
        }

        internal void HandleUdpPipeDisconnect(IPEndPoint from)
        {
            if (pipeMapUdp.TryRemove(from, out var to))
            {
                pipeMapUdp.TryRemove(to, out _);
            }
        }

        internal void HandleTcpPipeDisconnect(Guid from)
        {
            if (pipeMapTcp.TryRemove(from, out Guid to))
            {
                TcpServer.CloseSession(to);
                pipeMapTcp.TryRemove(to, out _);
            }

            tokenStorage.TryRemove(from, out _);
        }

        internal byte[] GetPipeToken( bool tcp)
        {
            byte[] data = new byte[PipeData.TokenLength];
            int offset = 0;
            PipeToken pipeData = new PipeToken();

            pipeData.Token = Guid.NewGuid();
            pipeData.Expiration = DateTime.UtcNow.AddMilliseconds(tokenLifetimeMs);

            PrimitiveEncoder.WriteGuid(data,ref offset, pipeData.Token);//16

            long Time = pipeData.Expiration.ToBinary();
            PrimitiveEncoder.WriteFixedInt64(data, ref offset, Time); //8

            byte[] signature = signer.Sign(data);
            Buffer.BlockCopy(signature, 0, data, offset, 32);//32

            if (tcp)
                RegisterTcpToken(pipeData);
            else
                RegisterUdpToken(pipeData);

            return data;
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

    }

    
}
