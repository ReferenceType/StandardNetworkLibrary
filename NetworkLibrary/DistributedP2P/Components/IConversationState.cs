using System;
using System.Threading.Tasks;

namespace NetworkLibrary.DistributedP2P.Components
{
    public interface IConversationState
    {
        Guid StateId { get; }
        bool IsSuccesful { get; }

        void HandleMessage(MessageEnvelope message);
        void Cancel();
        Task<IConversationState> WaitCompletion();

        event Action<IConversationState> OnComplete;


    }
}