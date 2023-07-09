using System.Threading;
using System.Threading.Tasks;



namespace NetworkLibrary.Utils
{
    public class AsyncManualResetEvent
    {
        private volatile TaskCompletionSource<bool> completionSource = new TaskCompletionSource<bool>();

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncManualResetEvent"/> class with
        /// a Boolean value indicating whether to set the initial state to signaled.
        /// </summary>
        /// <param name="signaled">if set to <c>true</c> [signaled].</param>
        public AsyncManualResetEvent(bool signaled)
        {
            if (signaled)
                completionSource.TrySetResult(true);
        }

        /// <summary>
        /// Waits the asynchronous.
        /// </summary>
        /// <returns></returns>
        public Task WaitAsync() { return completionSource.Task; }

        /// <summary>
        ///  Sets the state of the event to signaled, allowing one or more waiting tasks to proceed.
        /// </summary>
        public void Set() { completionSource.TrySetResult(true); }

        /// <summary>
        ///  Sets the state of the event to  non signaled, allowing one or more waiting tasks to await.
        /// </summary>
        public void Reset()
        {
            while (true)
            {
                var tcs = completionSource;
                if (!tcs.Task.IsCompleted ||
                    Interlocked.CompareExchange(ref completionSource, new TaskCompletionSource<bool>(), tcs) == tcs)
                    return;
            }
        }
    }
}
