using NetworkLibrary.Components.Crypto;
using NetworkLibrary.MessageProtocol;
using NetworkLibrary.P2P.Generic;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;

namespace NetworkLibrary.P2P.Components.StateManagement.Client
{
    internal class ClientStateManager<S> : StateManager where S : ISerializer, new()
    {
        internal RelayClientBase<S> client;
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
                case Constants.InitTCPHPRemote:
                    AddState(CreateTcpHolePunchStateRemote(message));
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

            var state = new ClientHolepunchState(message.From, message.MessageId, this, client.relayServerEndpoint, client.AESMode);
            state.Completed += (x) => { OnHolepunchComplete(state); };
            state.KeyReceived += (key, associatedEndpoints) => client.RegisterCrypto(key, associatedEndpoints, state.destinationId);
            state.InitiateByRemote(message);
            return state;
        }

        public ClientHolepunchState CreateHolePunchState(Guid clientId, Guid stateId)
        {
            pendingHolepunchStates.TryAdd(clientId, null);

            var state = new ClientHolepunchState(clientId, stateId, this, client.relayServerEndpoint, client.AESMode);
            state.Completed += (x) => { OnHolepunchComplete(state); };
            state.KeyReceived += (key, associatedEndpoints) => client.RegisterCrypto(key, associatedEndpoints, state.destinationId);
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
            pendingState = new ClientConnectionState(this,client.AESMode);
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
            var state = Interlocked.CompareExchange(ref pendingState, null, null);
            if (state == null)
            {
                return null;
            }
            state.StateId = message.MessageId;
            state.HandleMessage(message);
            AddState(state);
            return state;
        }


        internal bool IsHolepunchStatePending(Guid peerId)
        {
            return pendingHolepunchStates.TryGetValue(peerId, out _);
        }

        internal ClientTcpHolepunchState CreateTcpHolePunchState(Guid destinationId)
        {
            var state = new ClientTcpHolepunchState(this, client.relayServerEndpoint);
            state.InitiateByLocal(client.SessionId, destinationId, Guid.NewGuid());
            AddState(state);
            return state;
        }

        private ClientTcpHolepunchState CreateTcpHolePunchStateRemote(MessageEnvelope msg)
        {
            var state = new ClientTcpHolepunchState(this, client.relayServerEndpoint);
            state.InitiateByRemote(msg);

            state.Completed += (Istate) =>
            {
                if(state.Status == StateStatus.Completed)
                {
                    client.RegisterTcpNode(Istate);
                }
            };
            return state;
        }
    }
}
