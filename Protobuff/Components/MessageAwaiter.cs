using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using Protobuff;

namespace Protobuff
{
    #region Ignore
    public sealed class ReusableAwaiter<T> : INotifyCompletion
{
    private Action _continuation = null;
    private T _result = default(T);
    private Exception _exception = null;

    public bool IsCompleted
    {
        get;
        private set;
    }

    public T GetResult()
    {
        if (_exception != null)
            throw _exception;
        return _result;
    }

    public void OnCompleted(Action continuation)
    {
        if (_continuation != null)
            throw new InvalidOperationException("This ReusableAwaiter instance has already been listened");
        _continuation = continuation;
    }

    /// <summary>
    /// Attempts to transition the completion state.
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    public bool TrySetResult(T result)
    {
        if (!this.IsCompleted)
        {
            this.IsCompleted = true;
            this._result = result;

            if (_continuation != null)
                _continuation();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Attempts to transition the exception state.
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    public bool TrySetException(Exception exception)
    {
        if (!this.IsCompleted)
        {
            this.IsCompleted = true;
            this._exception = exception;

            if (_continuation != null)
                _continuation();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Reset the awaiter to initial status
    /// </summary>
    /// <returns></returns>
    public ReusableAwaiter<T> Reset()
    {
        this._result = default(T);
        this._continuation = null;
        this._exception = null;
        this.IsCompleted = false;
        return this;
    }

    public ReusableAwaiter<T> GetAwaiter()
    {
        return this;
    }
}

    public class Msg : IValueTaskSource<MessageEnvelope>
    {
        private static readonly Action<object> s_completedSentinel = new Action<object>(state => throw new Exception(nameof(s_completedSentinel)));
        /// <summary>Sentinel object used to indicate that the instance is available for use.</summary>
        private static readonly Action<object> s_availableSentinel = new Action<object>(state => throw new Exception(nameof(s_availableSentinel)));
        /// <summary>
        /// <see cref="s_availableSentinel"/> if the object is available for use, after GetResult has been called on a previous use.
        /// null if the operation has not completed.
        /// <see cref="s_completedSentinel"/> if it has completed.
        /// Another delegate if OnCompleted was called before the operation could complete, in which case it's the delegate to invoke
        /// when the operation does complete.
        /// </summary>
        private Action<object> _continuation = s_availableSentinel;
        private ExecutionContext _executionContext;
        private object _scheduler;
        /// <summary>Current token value given to a ValueTask and then verified against the value it passes back to us.</summary>
        /// <remarks>
        /// This is not meant to be a completely reliable mechanism, doesn't require additional synchronization, etc.
        /// It's purely a best effort attempt to catch misuse, including awaiting for a value task twice and after
        /// it's already being reused by someone else.
        /// </remarks>
        private short _token;
        private bool error;
        private MessageEnvelope result;

        public MessageEnvelope GetResult(short token)
        {
            if (token != _token)
            {
                // pop
            }

          
            return result;
        }

        public void SetResult(MessageEnvelope messageEnvelope) => result = messageEnvelope;

        public ValueTaskSourceStatus GetStatus(short token)
        {
            if (token != _token)
            {
                // halt and catch fire
            }

            return
                !ReferenceEquals(_continuation, s_completedSentinel) ? ValueTaskSourceStatus.Pending :
                error ? ValueTaskSourceStatus.Succeeded :
                ValueTaskSourceStatus.Faulted;
        }

        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            if (token != _token)
            {
                // throw
            }

            if ((flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) != 0)
            {
                _executionContext = ExecutionContext.Capture();
            }

            if ((flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) != 0)
            {
                SynchronizationContext sc = SynchronizationContext.Current;
                if (sc != null && sc.GetType() != typeof(SynchronizationContext))
                {
                    _scheduler = sc;
                }
                else
                {
                    TaskScheduler ts = TaskScheduler.Current;
                    if (ts != TaskScheduler.Default)
                    {
                        _scheduler = ts;
                    }
                }
            }
        }
    }

    #endregion
    internal class MessageAwaiter
    {
        MessageEnvelope timeoutResponse;
        ConcurrentDictionary<Guid, TaskCompletionSource<MessageEnvelope>> awaitingMessages = new ConcurrentDictionary< Guid, TaskCompletionSource<MessageEnvelope>>();

        public MessageAwaiter()
        {
            timeoutResponse = new MessageEnvelope()
            {
                Header = MessageEnvelope.RequestTimeout
            };
        }

        public async ValueTask<MessageEnvelope> RegisterWait(Guid messageId, int timeoutMs)
        {
            awaitingMessages[messageId] = new TaskCompletionSource<MessageEnvelope>();
            var pending = awaitingMessages[messageId].Task;

            MessageEnvelope returnMessage = null;
            if (await Task.WhenAny(pending, Task.Delay(timeoutMs)) == pending)
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
            if(envelopedMessage.MessageId!=null && awaitingMessages.TryGetValue(envelopedMessage.MessageId, out var completionSource))
                completionSource.TrySetResult(envelopedMessage);
        }

        internal bool IsWaiting(in Guid messageId)
        {
            return awaitingMessages.ContainsKey(messageId);
        }

        public void CancelWait(in Guid messageId)
        {

        }
    }
}
