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
namespace NetworkLibrary.DistributedP2P.Server
{
    internal class PipeAssociator
    {
        enum PipeFlag : byte
        {
            PipeAssociation = 1,
            RoomPipeAssociation = 2,
            PipeRoute = 3,
            RoomRoute = 4
        }
        internal AsyncTcpServer TcpServer;
        internal AsyncUdpServer UdpServer;

        internal ConcurrentDictionary<Guid, Guid> pipeMapTcp = new ConcurrentDictionary<Guid, Guid>();
        internal ConcurrentDictionary<IPEndPoint, IPEndPoint> pipeMapUdp = new ConcurrentDictionary<IPEndPoint, IPEndPoint>();

        ConcurrentDictionary<Guid, PipeState<Guid>> activeTcpPipeStates = new ConcurrentDictionary<Guid, PipeState<Guid>>();
        ConcurrentDictionary<Guid, PipeState<IPEndPoint>> activeUdpPipeStates = new ConcurrentDictionary<Guid, PipeState<IPEndPoint>>();

        byte[] cryptoKey;
        PrivateKeySign signer;
        private ITimeProvider timeProvider;

        public PipeAssociator(AsyncTcpServer tcpServer, AsyncUdpServer udpServer, byte[] pipeKey, ITimeProvider timeProvider)
        {
            TcpServer = tcpServer;
            UdpServer = udpServer;
            this.timeProvider = timeProvider;

            TcpServer.OnBytesReceived += HandleTcpBytes;
            UdpServer.OnBytesRecieved += HandleUdpBytes;
            signer = new PrivateKeySign(pipeKey);

        }


        private void HandleTcpBytes(Guid guid, byte[] bytes, int offset, int count)
        {
            if (!RouteTcp(guid, bytes, offset, count))
            {
                ManagePipeToken(guid, bytes, offset, count);
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
            var token = ExtractPipeToken(bytes, offset, count);
            if (token != null)
            {
                activeTcpPipeStates.TryGetValue(token.Token, out var state);
                state.RegisterClient(guid);
                if (state.IsComplete())
                {
                    TcpPipeCreated(state.Clients[0], state.Clients[1]);
                }
            }
            else
            {
                TcpServer.CloseSession(guid);
            }
        }

        //udp
        private void ManagePipeToken(IPEndPoint clientEp, byte[] bytes, int offset, int count)
        {
            var token = ExtractPipeToken(bytes, offset, count);
            if (token != null)
            {
                activeUdpPipeStates.TryGetValue(token.Token, out var state);
                state.RegisterClient(clientEp);
                if (state.IsComplete())
                {
                    UdpPipeCreated(state.Clients[0], state.Clients[1]);
                }
            }
            else
            {
                UdpServer.RemoveClient(clientEp);
            }
        }

        private PipeToken ExtractPipeToken(byte[] bytes, int offset, int count)
        {
            if (count < 24 + 32)
                throw new InvalidDataException();

            int offPrev = offset;
            var token = PrimitiveEncoder.ReadGuid(bytes, ref offset);//16
            var expiration = DateTime.FromBinary(PrimitiveEncoder.ReadFixedInt64(bytes, ref offset));//8

            var calculatedSignature = signer.Sign(bytes, offPrev, 24);

            if (SignatureMatch(calculatedSignature, bytes, offset))
            {
                return new PipeToken() { Token = token, Expiration = expiration };
            }
            else
            {
                return null;
            }
        }

        private bool SignatureMatch(byte[] localSignature, byte[] bytes, int offset)
        {
            for (int i = 0; i < localSignature.Length; i++)//32
            {
                if (localSignature[i] != bytes[offset + i])
                    return false;
            }
            return true;
        }

        internal void TcpPipeCreated(Guid from, Guid to)
        {
            pipeMapTcp.TryAdd(from, to);
            pipeMapTcp.TryAdd(to, from);
        }

        internal void HandleTcpPipeDisconnect(Guid from)
        {
            if (pipeMapTcp.TryRemove(from, out Guid to))
            {
                pipeMapTcp.TryRemove(to, out _);
            }
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

        internal void GetPipeData(PooledMemoryStream stream, bool tcp)
        {
            int originalPos = stream.Position32;
            PipeToken pipeData = new PipeToken();

            pipeData.Token = Guid.NewGuid();
            pipeData.Expiration = timeProvider.GetTime().AddSeconds(20);

            PrimitiveEncoder.WriteGuid(stream, pipeData.Token);//16
            long Time = pipeData.Expiration.ToBinary();
            PrimitiveEncoder.WriteFixedInt64(stream, Time); //8

            byte[] signature = signer.Sign(stream.GetBuffer(), originalPos, 24);
            stream.Write(signature, 0, signature.Length);

            if (tcp)
                activeTcpPipeStates.TryAdd(pipeData.Token, new PipeState<Guid>(pipeData));
            else
                activeUdpPipeStates.TryAdd(pipeData.Token, new PipeState<IPEndPoint>(pipeData));
        }
    }
}
