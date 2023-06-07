using NetworkLibrary;
using NetworkLibrary.Components;
using NetworkLibrary.Utils;
using Protobuff.P2P.HolePunch;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Protobuff.P2P.StateManagemet
{
    internal interface INetworkNode
    {
        void SendUdpAsync(IPEndPoint ep, MessageEnvelope message, Action<PooledMemoryStream> callback, ConcurrentAesAlgorithm aesAlgorithm);
        void SendUdpAsync(IPEndPoint ep, MessageEnvelope message, Action<PooledMemoryStream> callback);
        void SendAsyncMessage(Guid destinatioinId, MessageEnvelope message);
    }

    enum StateStatus
    {
        Pending,
        Completed,
        Failed
    }
    internal interface IState
    {
        StateStatus Status { get; }
        event Action<IState> Completed;
        Guid StateId { get; }
        void Release(bool isCompletedSuccessfully);
        void HandleMessage(MessageEnvelope message);
        void HandleMessage(IPEndPoint remoteEndpoint, MessageEnvelope message);
    }

    internal class StateManager
    {
       protected readonly ConcurrentDictionary<Guid, IState> activeStates = new ConcurrentDictionary<Guid, IState>();
        protected INetworkNode networkNode;

        public StateManager(INetworkNode networkNode)
        {
            this.networkNode = networkNode;
        }

        public void AddState(IState state)
        {
            if (state == null)
                return;
            if(activeStates.TryAdd(state.StateId, state))
            {
                StartLifetimeCounter(state.StateId);
                Console.WriteLine("State added");
            }
            else
            {
              //  Console.WriteLine("Dplicate state");
            }
        }

        public virtual bool HandleMessage(MessageEnvelope message)
        {
            if (activeStates.TryGetValue(message.MessageId, out var state))
            {
                state.HandleMessage(message);
                return true;
            }
            return false;
        }

        public bool HandleMessage(IPEndPoint remoteEndpoint, MessageEnvelope message)
        {
            if (activeStates.TryGetValue(message.MessageId, out var state))
            {
                state.HandleMessage(remoteEndpoint, message);
                return true;
            }
            return false;
        }

      

        private async void StartLifetimeCounter(Guid stateId)
        {
            await Task.Delay(20000);
            if (activeStates.TryRemove(stateId, out IState state))
            {
                state?.Release(false);
            }
        }
        internal void SendAsync(IPEndPoint ep, MessageEnvelope message, Action<PooledMemoryStream> callback, ConcurrentAesAlgorithm aesAlgorithm)
        {
            networkNode.SendUdpAsync(ep, message, callback, aesAlgorithm);
        }

        internal void SendAsync(IPEndPoint ep, MessageEnvelope message, Action<PooledMemoryStream> callback)
        {
            networkNode.SendUdpAsync(ep, message, callback);

        }

        internal void SendAsyncMessage(Guid destinatioinId, MessageEnvelope message)
        {
            networkNode.SendAsyncMessage(destinatioinId, message);

        }
    }

  
    internal class ServerStateManager : StateManager
    {
        SecureProtoRelayServer server;

        public ServerStateManager(SecureProtoRelayServer server):base(server)
        {
            this.server = server;
        }

        public  bool HandleMessage(Guid clientId, MessageEnvelope message)
        {
            switch (message.Header)
            {
                case Constants.Register:
                    AddState(CreateConnectionState(clientId,message));
                    return true;
                case Constants.HolePunchRequest:
                    AddState(CreateHolepunchState(message));
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
                Header = Constants.ServerCmd,
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

        private IState CreateHolepunchState(MessageEnvelope message)
        {
            bool encrypted = true;
            if (message.KeyValuePairs != null)
            {
                if (message.KeyValuePairs.TryGetValue("Encrypted", out string value))
                {
                    if (bool.TryParse(value, out var e))
                    {
                        encrypted = e;
                    }
                }
            }
            var state = new ServerHolepunchState(server, message.MessageId, message.From, message.To, encrypted);
            return state;
        }
    }

    internal class ClientStateManager : StateManager
    {
        RelayClientBase client;
        private ConcurrentDictionary<Guid, string> pendingHolepunchStates = new ConcurrentDictionary<Guid, string>();
        public ClientStateManager(RelayClientBase client):base(client)
        {
            this.client = client;
        }

        public sealed override bool HandleMessage(MessageEnvelope message)
        {
            switch (message.Header)
            {
                case Constants.CreateChannel:
                    AddState(CreateHolepunchState(message));
                    return true;
                case Constants.EndpointTransfer:
                    AddState(CreateHolepunchState2(message));
                    return true;
                default:
                    return base.HandleMessage(message);
            }
        }

        // soft punch
        private IState CreateHolepunchState2(MessageEnvelope message)
        {
            if(!pendingHolepunchStates.TryAdd(message.From, null))
            {
                base.HandleMessage(message);
                return null;
            }

            var state = new SimpleClientHPState(message.From, message.MessageId, this);
            state.Completed += (x)=> { OnHolepunchComplete2(state); };
            state.KeyReceived += (key, associatedEndpoints) => client.RegisterCrypto(key, associatedEndpoints);
            state.InitiateByRemote(message);
            return state;
        }

        private void OnHolepunchComplete2(IState obj)
        {
            if (obj.Status == StateStatus.Completed)
            {
                var state = obj as SimpleClientHPState;
                client.HandleCompletedHolepunchState2(state);
                pendingHolepunchStates.TryRemove(state.destinationId, out _);
            }
        }

        public SimpleClientHPState CreateHolePncState2(Guid clientId, Guid stateId)
        {
            pendingHolepunchStates.TryAdd(clientId, null);

            var state = new SimpleClientHPState(clientId, stateId, this);
            state.Completed += (x) => { OnHolepunchComplete2(state); };
            state.KeyReceived += (key, associatedEndpoints) => client.RegisterCrypto(key, associatedEndpoints);
            AddState(state);
            state.Initiate();
            return state;
        }

        // Hard punch
        private IState CreateHolepunchState(MessageEnvelope message)
        {
            if (activeStates.TryGetValue(message.MessageId, out var st))
            {
                base.HandleMessage(message);
                return st;
            }

            var chmsg = KnownTypeSerializer.DeserializeChanneCreationMessage(message.Payload, message.PayloadOffset);
            var state = new ClientHolepunchState(client, message.MessageId, chmsg.DestinationId, 10000, chmsg.Encrypted);
            state.HandleMessage(message);
            state.Completed += OnHolepunchComplete;
            return state;
        }

        private void OnHolepunchComplete(IState state_)
        {
            if (state_.Status == StateStatus.Completed)
            {
                var state = state_ as ClientHolepunchState;
                client.HandleCompletedHolepunchState(state);
            }
        }

        public ClientHolepunchState CreateHolepunchState(Guid targetId, int timeOut, bool encrypted = true)
        {
            Guid stateId = Guid.NewGuid();
            ClientHolepunchState state = new ClientHolepunchState(client, stateId, targetId, timeOut, encrypted);
            AddState(state);
           
            var request = new MessageEnvelope()
            {
                Header = Constants.HolePunchRequest,
                IsInternal = true,
                MessageId = stateId,
                KeyValuePairs = new Dictionary<string, string>() { { "Encrypted", encrypted.ToString() } }

            };
            client.SendAsyncMessage(targetId, request);

            return state;

        }

        internal bool IsHolepunchStatePending(Guid peerId)
        {
           return pendingHolepunchStates.TryGetValue(peerId, out _);
        }
    }
}
