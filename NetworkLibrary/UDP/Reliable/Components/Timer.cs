using NetworkLibrary.Utils;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;



namespace NetworkLibrary.UDP.Reliable.Components
{
    public class Timer
    {
        static readonly ConcurrentDictionary<TimedObject, string> activeTimers = new ConcurrentDictionary<TimedObject, string>();
        public static int TickFrequency = 10;

        static Timer()
        {
            TimedObject.activeTimers = activeTimers;
            StartTimer();
        }

        private static void StartTimer()
        {
            Looper();
        }
        static Stopwatch time = new Stopwatch();
        private static void Looper()
        {
#if Debug
            Task.Run(async () => {
                while (true)
                {
                    await Task.Delay(1000);
                    Console.WriteLine("pending timers : " + activeTimers.Count);
                   
                }

            });
#endif
            Thread t = new Thread(() =>
            {
                time.Start();
                while (true)
                {
                    Thread.Sleep(TickFrequency);
                    try
                    {
                        Tick((int)time.ElapsedMilliseconds);
                        time.Restart();
                    }
                    catch { }
                }

            });
            t.Start();

        }
        private static object locker = new object();
        private static void Tick(int dt)
        {
            if (activeTimers.Count == 0)
                return;

            foreach (var item in activeTimers)
            {
                item.Key.Tick(dt);
            }

            // Parallel.ForEach(activeTimers,t=>t.Key.Tick(dt));
        }



        public static bool Register(TimedObject instance)
        {
            if (!activeTimers.TryAdd(instance, null))
            {
                MiniLogger.Log(MiniLogger.LogLevel.Error, "Unable to add timer instance, there is a serious concurrency error " + instance.GetType().Name);
                return false;
            }
            return true;
        }

        public static bool Remove(TimedObject instance)
        {
            if (!activeTimers.TryRemove(instance, out _))
            {
                //  MiniLogger.Log(MiniLogger.LogLevel.Error, "Unable to Remove timer instance, there is a serious concurrency error"+ instance.GetType().Name);
                return false;
            }
            return true;
        }

    }

    public abstract class TimedObject
    {
        public static ConcurrentDictionary<TimedObject, string> activeTimers;

        public abstract void Tick(int dt);


        protected virtual void OnTimeout()
        {
            activeTimers.TryRemove(this, out _);
        }

    }

}
