using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace NetworkLibrary.DistributedP2P.Components
{
    internal class PreciseTimeAwaiter
    {
       static Stopwatch sw = Stopwatch.StartNew();
        public static void Wait(double miliseconds)
        {
            if (miliseconds <= 0)
                return;
            double time = sw.Elapsed.TotalMilliseconds;
            double until = time+miliseconds;

            while (until > sw.Elapsed.TotalMilliseconds)
            {
                if((until - sw.Elapsed.TotalMilliseconds)>32)
                    Thread.Sleep(10);//~16 ms
                else
                    Thread.SpinWait(20);
            }

        }
    }
}
