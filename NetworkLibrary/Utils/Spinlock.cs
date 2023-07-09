using System.Runtime.CompilerServices;
using System.Threading;

namespace NetworkLibrary.Utils
{
    public class Spinlock
    {
        private int lockValue = 0;
        SpinWait spinWait = new SpinWait();


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Take()
        {
            if (Interlocked.CompareExchange(ref lockValue, 1, 0) != 0)
            {
                int spinCount = 0;

                while (Interlocked.CompareExchange(ref lockValue, 1, 0) != 0)
                {

                    if (spinCount < 22)
                    {
                        spinCount++;
                    }

                    else
                    {
                        spinWait.SpinOnce();
                    }
                }
            }

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsTaken() => Interlocked.CompareExchange(ref lockValue, 1, 1) == 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Release()
        {
            Interlocked.Exchange(ref lockValue, 0);
        }

        internal bool TryTake()
        {
            return Interlocked.CompareExchange(ref lockValue, 1, 0) == 0;
        }
    }
}
