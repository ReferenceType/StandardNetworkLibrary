using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkLibrary.Utils
{
    internal class AsyncDispatcher
    {
        TaskCompletionSource<bool> signal = new TaskCompletionSource<bool>();
        int cancel = 0;
        public void Signal()
        {
            signal.TrySetResult(true);
        }

        public void Abort() 
        {
            Interlocked.Increment(ref cancel);
            signal.TrySetResult(true);
        }

        public async Task ExecuteBySignalEfficient(Action execution, int delayBetweenMs = 1000)
        {
            while (true)
            {
                try
                {
                    if (Interlocked.CompareExchange(ref cancel, 0, 0) > 0) return;
                    await signal.Task.ConfigureAwait(false);
                    if (Interlocked.CompareExchange(ref cancel, 0, 0) > 0)return;

                    Interlocked.Exchange(ref signal, new TaskCompletionSource<bool>());

                    execution?.Invoke();
                    await Task.Delay(delayBetweenMs).ConfigureAwait(false);
                }
                catch(Exception e)
                {
                    throw new AggregateException(e);
                }
            }
        }

        public async Task ExecuteBySignal(Action execution)
        {
            while (true)
            {
                try
                {
                    if (Interlocked.CompareExchange(ref cancel, 0, 0) > 0) return;
                    await signal.Task.ConfigureAwait(false);
                    if (Interlocked.CompareExchange(ref cancel, 0, 0) > 0) return;

                    Interlocked.Exchange(ref signal, new TaskCompletionSource<bool>());

                    execution?.Invoke();
                }
                catch (Exception e)
                {
                    throw new AggregateException(e);
                }
            }
        }

        public async Task LoopPeriodic(Action execution, int ms)
        {
            Stopwatch sw =  new Stopwatch();
            while (true)
            {
                try
                {
                    if (Interlocked.CompareExchange(ref cancel, 0, 0) > 0) return;

                    sw.Stop();
                    int sleep = ms-(int)sw.ElapsedMilliseconds;
                    sleep = Math.Max(sleep, 0);
                    await Task.Delay(ms).ConfigureAwait(false);
                  
                    if (Interlocked.CompareExchange(ref cancel, 0, 0) > 0) return;

                    sw.Restart();
                    execution.Invoke();
                }
                catch (Exception e)
                {
                    throw new AggregateException(e);
                }


            }
        }
        //Func<T, Task>

        public async Task LoopPeriodicTask(Func<Task> asyncTask, int ms)
        {
            while (true)
            {
                try
                {
                    if (Interlocked.CompareExchange(ref cancel, 0, 0) > 0) return;
                        await Task.Delay(ms).ConfigureAwait(false);

                    if (Interlocked.CompareExchange(ref cancel, 0, 0) > 0) return;
                        await asyncTask.Invoke().ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    throw new AggregateException(e);
                }


            }
        }
    }
}
