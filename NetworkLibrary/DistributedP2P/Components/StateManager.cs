using System;
using System.Collections.Concurrent;
using static NetworkLibrary.P2P.Components.PingData;

namespace NetworkLibrary.DistributedP2P.Components
{
    internal class StateManager
    {
        ConcurrentDictionary<Guid, IConversationState> states = new ConcurrentDictionary<Guid, IConversationState>();
        public void RegisterState(IConversationState state)
        {
            state.OnComplete += HandleComplete;
            states.TryAdd(state.StateId, state);

        }

        private void HandleComplete(IConversationState state)
        {
            UnregisterState(state.StateId);
            state.OnComplete -= HandleComplete;
        }

        public void UnregisterState(Guid stateId)
        {
            states.TryRemove(stateId, out _);
        }

        public bool HandleMessage(MessageEnvelope message)
        {
            Guid stateId = message.MessageId;

            if (stateId == Guid.Empty)
            {
                return false;
            }

            if (states.TryGetValue(stateId, out var state))
            {
                try
                {
                    state.HandleMessage(message);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"State Management failed{e.Message}\n{e.StackTrace}");
                    state.Cancel();
                    UnregisterState(stateId);
                }
                return true;
            }

            return false;
        }


    }
}
