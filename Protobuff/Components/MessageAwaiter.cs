using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Protobuff
{

    internal class MessageAwaiter
    {
        MessageEnvelope timeoutResponse;
        ConcurrentDictionary<Guid, TaskCompletionSource<MessageEnvelope>> awaitingMessages
            = new ConcurrentDictionary<Guid, TaskCompletionSource<MessageEnvelope>>();
        public MessageAwaiter()
        {
            timeoutResponse = new MessageEnvelope()
            {
                Header = MessageEnvelope.RequestTimeout
            };
        }

        public async Task<MessageEnvelope> RegisterWait(Guid messageId, int timeoutMs)
        {
            awaitingMessages[messageId] = new TaskCompletionSource<MessageEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = awaitingMessages[messageId].Task;
            MessageEnvelope returnMessage;

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

        public void ResponseArrived(MessageEnvelope envelopedMessage)
        {
            if (envelopedMessage.MessageId != null && awaitingMessages.TryGetValue(envelopedMessage.MessageId, out var completionSource))
            {
                // lock(do a copy) because continiation will be async.
                envelopedMessage.LockBytes();
                completionSource.TrySetResult(envelopedMessage);
            }
        }

        internal bool IsWaiting(in Guid messageId)
        {
            return awaitingMessages.TryGetValue(messageId, out _);
        }

        public void CancelWait(in Guid messageId)
        {
            //todo
        }
    }
}
