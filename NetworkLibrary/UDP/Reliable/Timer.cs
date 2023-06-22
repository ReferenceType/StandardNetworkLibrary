using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Timers;
using NetworkLibrary.Utils;

namespace NetworkLibrary.UDP.Reliable
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
        private static void Looper()
        {
            Task.Run(async () => {
                while (true)
                {
                    await Task.Delay(1000);
                    Console.WriteLine("pending timers : " + activeTimers.Count);
                   
                }

            });
            Thread t = new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(TickFrequency);
                    try
                    {
                        Tick();
                    }
                    catch { }
                }

            });
            t.Start();

        }
        private static object locker =  new object();
        private static void Tick()
        {
            if (activeTimers.Count == 0)
                return;
            
                foreach (var item in activeTimers)
                {
                    item.Key.Tick();
                }
            
           
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
            if(!activeTimers.TryRemove(instance, out _))
            {
                MiniLogger.Log(MiniLogger.LogLevel.Error, "Unable to Remove timer instance, there is a serious concurrency error"+ instance.GetType().Name);
                return false;
            }
            return true;
        }

    }

    public abstract class TimedObject 
    {
        public static ConcurrentDictionary<TimedObject, string> activeTimers;

        public abstract void Tick();
        

        protected virtual void OnTimeout()
        {
            activeTimers.TryRemove(this, out _);
        }
      
    }

}
