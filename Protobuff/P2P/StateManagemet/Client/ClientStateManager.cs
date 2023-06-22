using NetworkLibrary;
using NetworkLibrary.MessageProtocol;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Protobuff.P2P.StateManagemet.Client
{
    internal class ClientStateManager<S> : StateManager where S : ISerializer,new()
    {
        private RelayClientBase<S> client;
        private ConcurrentDictionary<Guid, string> pendingHolepunchStates = new ConcurrentDictionary<Guid, string>();
        private ClientConnectionState pendingState;
        public ClientStateManager(RelayClientBase<S> client) : base(client)
        {
            this.client = client;
        }

        public sealed override bool HandleMessage(MessageEnvelope message)
        {
            switch (message.Header)
            {
                case Constants.ServerRegisterAck:
                    AddState(UpdateConnectionState(message));
                    return true;
                case Constants.InitiateHolepunch:
                    AddState(CreateHolepunchState(message));
                    return true;
                default:
                    return base.HandleMessage(message);
            }
        }

        private IState CreateHolepunchState(MessageEnvelope message)
        {
            if (!pendingHolepunchStates.TryAdd(message.From, null))
            {
                base.HandleMessage(message);
                return null;
            }

            var state = new ClientHolepunchState(message.From, message.MessageId, this);
            state.Completed += (x) => { OnHolepunchComplete(state); };
            state.KeyReceived += (key, associatedEndpoints) => client.RegisterCrypto(key, associatedEndpoints);
            state.InitiateByRemote(message);
            return state;
        }

        public ClientHolepunchState CreateHolePunchState(Guid clientId, Guid stateId)
        {
            pendingHolepunchStates.TryAdd(clientId, null);

            var state = new ClientHolepunchState(clientId, stateId, this);
            state.Completed += (x) => { OnHolepunchComplete(state); };
            state.KeyReceived += (key, associatedEndpoints) => client.RegisterCrypto(key, associatedEndpoints);
            AddState(state);
            state.Initiate();
            return state;
        }

        private void OnHolepunchComplete(IState obj)
        {
            if (obj.Status == StateStatus.Completed)
            {
                var state = obj as ClientHolepunchState;
                client.HandleHolepunchSuccess(state);
                pendingHolepunchStates.TryRemove(state.destinationId, out _);
            }
            else
            {
                var state = obj as ClientHolepunchState;
                client.HandleHolepunchFailure(state);
                pendingHolepunchStates.TryRemove(state.destinationId, out _);
            }
        }

        // create a temp state bc we dont know state id yet
        public ClientConnectionState CreateConnectionState()
        {
            pendingState = new ClientConnectionState(this);
            pendingState.serverEndpoint = client.relayServerEndpoint;
            pendingState.localEndpoints = client.GetLocalEndpoints();

            client.tcpMessageClient.SendAsyncMessage(new MessageEnvelope()
            {
                IsInternal = true,
                Header = Constants.Register,
            });
            return pendingState;
        }
        // here server will send us necessary info
        private IState UpdateConnectionState(MessageEnvelope message)
        {
            var state = Interlocked.Exchange(ref pendingState,null);
            state.StateId = message.MessageId;
            state.HandleMessage(message);
            AddState(state);
            return state;
        }

       
        internal bool IsHolepunchStatePending(Guid peerId)
        {
            return pendingHolepunchStates.TryGetValue(peerId, out _);
        }
    }
}
