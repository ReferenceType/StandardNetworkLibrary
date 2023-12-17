using NetworkLibrary.MessageProtocol;
using NetworkLibrary.P2P.Components.StateManagemet.Server;
using NetworkLibrary.P2P.Generic;
using NetworkLibrary.Utils;
using System;

namespace NetworkLibrary.P2P.Components.StateManagement.Server
{
    internal class ServerStateManager<S> : StateManager where S : ISerializer, new()
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
                    CreateConnectionState(clientId, message);
                    return true;
                case Constants.ReqTCPHP:
                    CreateTcpHPState(message);
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
            AddState(state);
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

        private ServerTcpHolepunchState CreateTcpHPState(MessageEnvelope message)
        {
            var state = new ServerTcpHolepunchState(this,message.MessageId);
            AddState(state);
            state.Initialize(message);
            MiniLogger.Log(MiniLogger.LogLevel.Info, "Created tcp holepunch state");
            // room server needs to know this completion. maybe?
            return state;
        }
    }
}
