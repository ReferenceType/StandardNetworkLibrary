using NetworkLibrary.Components;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading.Tasks;

namespace NetworkLibrary.P2P.Components.StateManagement
{
    internal interface INetworkNode
    {
        
        void SendUdpAsync(IPEndPoint ep, MessageEnvelope message, Action<PooledMemoryStream> callback, ConcurrentAesAlgorithm aesAlgorithm);
        void SendUdpAsync(IPEndPoint ep, MessageEnvelope message, Action<PooledMemoryStream> callback);
        void SendAsyncMessage(Guid destinatioinId, MessageEnvelope message);
        void SendAsyncMessage(Guid destinatioinId, MessageEnvelope message, Action<PooledMemoryStream> callback);

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
            if (activeStates.TryAdd(state.StateId, state))
            {
                StartLifetimeCounter(state.StateId);
                
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
        internal void SendUdpAsync(IPEndPoint ep, MessageEnvelope message, Action<PooledMemoryStream> callback, ConcurrentAesAlgorithm aesAlgorithm)
        {
            networkNode.SendUdpAsync(ep, message, callback, aesAlgorithm);
        }

        internal void SendUdpAsync(IPEndPoint ep, MessageEnvelope message, Action<PooledMemoryStream> callback)
        {
            networkNode.SendUdpAsync(ep, message, callback);
        }

        internal void SendTcpMessage(Guid destinationId, MessageEnvelope message)
        {
            networkNode.SendAsyncMessage(destinationId, message);
        }

        internal void SendTcpMessage(Guid destinationId, MessageEnvelope message, Action<PooledMemoryStream> callback)
        {
            networkNode.SendAsyncMessage(destinationId, message,callback);
        }
    }
}
