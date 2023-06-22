using NetworkLibrary;
using NetworkLibrary.MessageProtocol;
using ProtoBuf;
using Protobuff.Components.Serialiser;
using Protobuff.P2P.HolePunch;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Protobuff.P2P.StateManagemet.Server
{

    class ServerConnectionState : IState
    {

        public event Action<IState> Completed;
        public Guid StateId { get; }

        public StateStatus Status => currentStatus;

        internal Guid clientId;
        internal IPEndPoint remoteEndpoint;
        internal EndpointTransferMessage endpointTransferMsg;
        internal byte[] random;

        private int currentState = 0;
        private StateStatus currentStatus = StateStatus.Pending;
        private StateManager server;

        public ServerConnectionState(Guid clientId, Guid stateId, StateManager server)
        {
            this.server = server;
            this.clientId = clientId;
            StateId = stateId;
        }

        public void HandleMessage(MessageEnvelope message)
        {
            if (message.Header == Constants.ClientFinalizationAck)
            {
                HandleClientFinalization(message);
            }
        }
        public void HandleMessage(IPEndPoint remoteEndpoint, MessageEnvelope message)
        {
            if (message.Header == Constants.UdpInit)
            {
                HandleUdpInitMessage(remoteEndpoint, message);
            }
        }

        public void HandleUdpInitMessage(IPEndPoint remoteEndpoint, MessageEnvelope message)
        {
            if (Interlocked.CompareExchange(ref currentState, 1, 0) == 0)
            {
                this.remoteEndpoint = remoteEndpoint;
                endpointTransferMsg = KnownTypeSerializer.DeserializeEndpointTransferMessage(message.Payload, message.PayloadOffset);

                random = new byte[16];
                RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
                rng.GetNonZeroBytes(random);

                MessageEnvelope envelope = new MessageEnvelope
                {
                    IsInternal = true,
                    Header = Constants.ServerFinalizationCmd,
                    Payload = random,
                    MessageId = StateId,
                };

                server.SendAsyncMessage(clientId, envelope);
            }
        }

        public void HandleClientFinalization(MessageEnvelope message)
        {
            Release(true);
        }

        private int isReleased = 0;
        public void Release(bool isCompletedSuccessfully)
        {
            if (Interlocked.CompareExchange(ref isReleased, 1, 0) == 0)
            {
                if (isCompletedSuccessfully)
                    currentStatus = StateStatus.Completed;
                else
                    currentStatus = StateStatus.Failed;
                Completed?.Invoke(this);
                Completed = null;
            }
        }
    }
}
