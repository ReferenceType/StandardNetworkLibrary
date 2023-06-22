using NetworkLibrary;
using NetworkLibrary.MessageProtocol;
using System;

namespace Protobuff.P2P.StateManagemet.Server
{
    internal class ServerStateManager<S> : StateManager where S : ISerializer,new()
    {
        SecureRelayServerBase<S> server;

        public ServerStateManager(SecureRelayServerBase<S> server) : base(server)
        {
            this.server = server;
        }

        public bool HandleMessage(Guid clientId, MessageEnvelope message)
        {
            switch (message.Header)
            {
                case Constants.Register:
                    AddState(CreateConnectionState(clientId, message));
                    return true;

                default:
                    return base.HandleMessage(message);
            }

        }


        // Connection state
        private IState CreateConnectionState(Guid clientId, MessageEnvelope message)
        {
            var stateId = Guid.NewGuid();
            var state = new ServerConnectionState(clientId, stateId, this);
            state.Completed += OnServerConnectionStateCompleted;
            MessageEnvelope envelope = new MessageEnvelope()
            {
                IsInternal = true,
                Header = Constants.ServerRegisterAck,
                To = clientId,
                MessageId = stateId,
                Payload = server.ServerUdpInitKey
            };

            server.SendAsyncMessage(clientId, envelope);
            return state;
        }

        private void OnServerConnectionStateCompleted(IState obj)
        {
            if (obj.Status == StateStatus.Completed)
            {
                var state = obj as ServerConnectionState;
                server.Register(state.clientId, state.remoteEndpoint, state.endpointTransferMsg.LocalEndpoints, state.random);
            }
        }
    }
}
