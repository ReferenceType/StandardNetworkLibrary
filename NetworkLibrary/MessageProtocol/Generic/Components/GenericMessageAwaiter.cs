using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace NetworkLibrary.MessageProtocol
{
    public class GenericMessageAwaiter<E> where E : IMessageEnvelope, new()
    {
        E timeoutResponse;
        ConcurrentDictionary<Guid, TaskCompletionSource<E>> awaitingMessages
            = new ConcurrentDictionary<Guid, TaskCompletionSource<E>>();
        public GenericMessageAwaiter()
        {
            timeoutResponse = new E();
            timeoutResponse.Header = "RequestTimedOut";
        }

        public async Task<E> RegisterWait(Guid messageId, int timeoutMs)
        {
            awaitingMessages[messageId] = new TaskCompletionSource<E>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = awaitingMessages[messageId].Task;
            E returnMessage;

            var delay = Task.Delay(timeoutMs);
            if (await Task.WhenAny(pending, delay).ConfigureAwait(false) == pending)
            {
                // Task completed within timeout.
                returnMessage = pending.Result;
            }
            else
            {
                // timeout/cancellation logic
                returnMessage = timeoutResponse;

            }

            awaitingMessages.TryRemove(messageId, out _);
            return returnMessage;
        }

        public void ResponseArrived(E envelopedMessage)
        {
            if (envelopedMessage.MessageId != null && awaitingMessages.TryGetValue(envelopedMessage.MessageId, out var completionSource))
            {
                // lock(do a copy) because continiation will be async.
                envelopedMessage.LockBytes();
                completionSource.TrySetResult(envelopedMessage);
            }
        }

        public bool IsWaiting(Guid messageId)
        {
            return awaitingMessages.TryGetValue(messageId, out _);
        }

        public void CancelWait(Guid messageId)
        {
            //todo
        }
    }
}
