using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace NetworkLibrary.Utils
{
    public class AsyncAutoResetEvent
    {
        private readonly static Task completed = Task.FromResult(true);
        private readonly ConcurrentQueue<TaskCompletionSource<bool>> waiters = new ConcurrentQueue<TaskCompletionSource<bool>>();
        private bool signaled;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncAutoResetEvent"/>
        /// class with a Boolean value indicating whether to set the initial state to signaled.
        /// </summary>
        /// <param name="signaled">if set to <c>true</c> [signaled].</param>
        public AsyncAutoResetEvent(bool signaled = false)
        {
            this.signaled = signaled;
        }


        /// <summary>
        ///    Switches state machine of the current task until the event is signaled,
        ///    a signal.
        /// </summary>
        /// <returns></returns>
        public Task WaitAsync()
        {
            lock (waiters)
            {
                if (signaled)
                {
                    signaled = false;
                    return completed;
                }
                else
                {
                    var tcs = new TaskCompletionSource<bool>();
                    waiters.Enqueue(tcs);
                    return tcs.Task;
                }
            }
        }

        /// <summary>
        /// Sets the state of the event to signaled, allowing one or more waiting tasks to proceed.
        /// </summary>
        public void Set()
        {
            TaskCompletionSource<bool> toRelease = null;
            lock (waiters)
            {
                if (waiters.Count > 0)
                {
                    waiters.TryDequeue(out toRelease);
                }

                else if (!signaled)
                    signaled = true;

                if (toRelease != null)
                    toRelease.SetResult(true);
            }

        }
    }
}
