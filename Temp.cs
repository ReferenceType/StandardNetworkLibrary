using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ExpressionTree
{
    public class Timer
    {
        static readonly ConcurrentDictionary<TimerInstance, string> activeTimers = new ConcurrentDictionary<TimerInstance, string>();
        static readonly ConcurrentBag<TimerInstance> timerPool =  new ConcurrentBag<TimerInstance>();

        static readonly AutoResetEvent a = new AutoResetEvent(false);
        static int Number = 0;
        static Timer()
        {
            TimerInstance.rec = activeTimers;
            StartTimerThread();
        }

        private static void StartTimerThread()
        {
            Thread t = new Thread(() => 
            {
                SpinWait sw = new SpinWait();
                Stopwatch stopwatch = new Stopwatch();
                while (true)
                {
                    a.WaitOne();
                    stopwatch.Restart();
                    Thread.Sleep(10);
                    //sw.SpinOnce();
                    //stopwatch.Stop();

                    Tick(stopwatch);
                }
            });

            // t.Start();
            Looper();
            
        }
        private async static void Looper()
        {
            Stopwatch stopwatch = new Stopwatch();
            while (true)
            {
             
                stopwatch.Restart();
                await Task.Delay(10).ConfigureAwait(false);
                Tick(stopwatch);
            }
        }

        private static void Tick(Stopwatch sw)
        {
            if(activeTimers.Count == 0)
                return;

            a.Set();
            foreach (var item in activeTimers)
            {
                item.Key.Tick((int)sw.ElapsedMilliseconds);
            }
        }

        private static TimerInstance GetTimer()
        {
            var timer = new TimerInstance();
            timer.number = Interlocked.Increment(ref Number);
            return timer;
        }

        public static void Wait(int miliseconds,Action OnCompleted)
        {
            var timer = GetTimer();
            timer.remaining = miliseconds;
            timer.Completed += OnCompleted;
            activeTimers.TryAdd(timer, null);
            a.Set();
        }

        class TimerInstance
        {
            public int remaining;
            public Action Completed;
            public static ConcurrentDictionary<TimerInstance, string> rec;
            public static ConcurrentBag<TimerInstance> timerPool;
            public int number;
            public void Tick(int dt)
            {
                remaining -= dt;
                if (remaining <= 0)
                {
                    Complete();
                }
            }

            private void Complete()
            {
                rec.TryRemove(this, out _);
                Completed?.Invoke();
                Reset();
            }

            public void Reset()
            {
                //remaining = 1000;
                Completed = null;
            }
            public override int GetHashCode()
            {
                return number;
            }
        }
    }
}
