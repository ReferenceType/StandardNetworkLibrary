using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace NetworkLibrary.Utils
{
    public class Spinlock
    {
        private int lockValue=0;

        

        public void Take()
        {
            while(Interlocked.CompareExchange(ref lockValue, 1, 0) != 0)
            {
                //Thread.SpinWait(2);
            }
        }

        public void Release()
        {
            Interlocked.Exchange(ref lockValue, 0);
        }
    }
}
