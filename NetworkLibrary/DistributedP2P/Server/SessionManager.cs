using NetworkLibrary.Components;
using NetworkLibrary.Components.Crypto.DigitalSignature;
using NetworkLibrary.DistributedP2P.Components;
using NetworkLibrary.MessageProtocol;
using NetworkLibrary.MessageProtocol.Serialization;
using NetworkLibrary.P2P.Components.HolePunch;
using NetworkLibrary.TCP.Base;
using NetworkLibrary.UDP;
using NetworkLibrary.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace NetworkLibrary.DistributedP2P.Server
{
    class Flags
    {
        public const byte Route = 1;
    }


    class PipeToken
    {
        public Guid Token;
        public DateTime Expiration;
    }

    // Creation, and managing destruction of sessions.
    //Created after Authentication.
    // Routing messages between sessions.
    // Where do we put rooms? probably here
    internal class SessionManager<S> where S : ISerializer, new()
    {
        internal ConcurrentDictionary<Guid, ServerSession> serverSessions = new ConcurrentDictionary<Guid, ServerSession>();
        internal ConcurrentDictionary<EndPoint, Guid> endPointMap = new ConcurrentDictionary<EndPoint, Guid>();
        internal ConcurrentDictionary<Guid, Guid> pipeMapTcp = new ConcurrentDictionary<Guid, Guid>();
        internal ConcurrentDictionary<IPEndPoint, IPEndPoint> pipeMapUdp = new ConcurrentDictionary<IPEndPoint, IPEndPoint>();

        PipeAssociator piper;
        RandomNumberGenerator random;

        IDistributedConnection serverConnection;

        public SessionManager(IDistributedConnection serverConnection, AsyncTcpServer tcpServer, AsyncUdpServer udpServer, byte[] pipeKey)
        {
            random = RandomNumberGenerator.Create();
            piper = new PipeAssociator(tcpServer, udpServer, pipeKey, serverConnection);
            this.serverConnection = serverConnection;
        }


        internal bool HandleMessage(Guid from, MessageEnvelope envelope)
        {
            switch (envelope.Header)
            {
                case "Pipe":
                    ManagePiping(from, envelope.To, envelope);
                    break;
            }
            return false;
        }

        private async void ManagePiping(Guid A, Guid B, MessageEnvelope msg)
        {

            PooledMemoryStream tokenData = await FindOptimumTcpServer();

            MessageEnvelope envelope = new MessageEnvelope();
            envelope.Header = "PipeToken";
            envelope.MessageId = msg.MessageId;
            envelope.SetPayload(tokenData.GetBuffer(), 0, tokenData.Position32);

            Task<MessageEnvelope> ackA = serverConnection.SendMessageAndWaitResponse(A, envelope);
            Task<MessageEnvelope> ackB = serverConnection.SendMessageAndWaitResponse(B, envelope);

            await Task.WhenAll(ackA, ackB);

            MessageEnvelope finalAck = new MessageEnvelope();
            finalAck.MessageId = msg.MessageId;
            finalAck.Header = "Fail";

            if (isGood(ackA.Result) && isGood(ackB.Result))
            {
                finalAck.Header = "Success";
                serverConnection.SendAsyncMessage(A, finalAck);
                serverConnection.SendAsyncMessage(B, finalAck);
            }
            else
            {
                serverConnection.SendAsyncMessage(A, finalAck);
                serverConnection.SendAsyncMessage(B, finalAck);
            }

            SharerdMemoryStreamPool.ReturnStreamStatic(tokenData);
        }

        private bool isGood(MessageEnvelope ackA)
        {
            if (ackA.Header != MessageEnvelope.RequestTimeout)
                return true;
            return false;
        }

        private Task<PooledMemoryStream> FindOptimumTcpServer()
        {
            //do only local for now
            PooledMemoryStream stream = SharerdMemoryStreamPool.RentStreamStatic();
            piper.GetPipeData(stream, tcp: true);
            return Task.FromResult(stream);
        }

        public void CreateSession(IClientDbInfo clientInfo, Guid ephemeralClientId, IPEndPoint sessionEp)
        {
            serverSessions.TryAdd(ephemeralClientId, new ServerSession(clientInfo));
            //Send a feedback
        }

        internal void DestroySession(Guid guid)
        {

        }

    }
}
