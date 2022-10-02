using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace NetworkLibrary.Utils
{
    internal class Spinlock
    {
        int lockVariable=0;

        public void Take()
        {
            while(Interlocked.CompareExchange(ref lockVariable, 1, 0) != 0)
            {
                //Thread.SpinWait(2);
            }
        }

        public void Release()
        {
            Interlocked.Exchange(ref lockVariable, 0);
        }
    }
}
