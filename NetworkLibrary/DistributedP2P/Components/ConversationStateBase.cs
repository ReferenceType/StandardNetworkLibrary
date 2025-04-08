using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkLibrary.DistributedP2P.Components
{
    internal abstract class ConversationStateBase:IConversationState
    {
        public Guid StateId { get; private set; }

        public bool IsSuccesful { get; private set; }

        public event Action<IConversationState> OnComplete;

        public string ErrorMessage { get; protected set; }

        protected readonly object cancellationMutex = new object();
        private TaskCompletionSource<IConversationState> Completion;
        private int isComplete = 0;

        public ConversationStateBase(Guid stateId, int timeout = -1)
        {
            this.StateId = stateId;
            Completion = new TaskCompletionSource<IConversationState>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (timeout > 0)
            {
                TimerService.RegisterTimer(stateId, timeout, OnTimeOut);
            }
        }

        private void OnTimeOut()
        {
            if (!IsCompleted())
            {
                Cancel();
            }
        }

        public abstract void HandleMessage(MessageEnvelope message);

        public Task<IConversationState> WaitCompletion()
        {
            return Completion.Task;
        }

        public virtual void Cancel()
        {
            lock (cancellationMutex)
            {
                Completed(false);
            }

        }
        protected virtual MessageEnvelope CreateErrorMsg(string err)
        {
            var msg = CreateEnvelope();
            msg.Header = InternalConstants.Error;
            msg.KeyValuePairs = new Dictionary<string, string>
            {
                { "Error", err }
            };
            return msg;
        }
        protected virtual MessageEnvelope CreateEnvelope()
        {
            MessageEnvelope msg = new MessageEnvelope();
            msg.MessageId = StateId;
            msg.IsInternal = true;
            return msg;
        }

        protected bool IsCompleted()
        {
            return Interlocked.CompareExchange(ref isComplete, 0, 0) == 1;
        }


        protected virtual void Completed(bool succes)
        {
            if (Interlocked.CompareExchange(ref isComplete, 1, 0) == 0)
            {

                IsSuccesful = succes;
                OnComplete?.Invoke(this);
                Completion.SetResult(this);
                OnComplete = null;
            }
        }

       
    }
}
