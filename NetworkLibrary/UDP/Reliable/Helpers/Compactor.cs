using NetworkLibrary.Components;
using System;

namespace NetworkLibrary.UDP.Reliable.Helpers
{
    class Compactor
    {
        PooledMemoryStream prev;
        PooledMemoryStream stream = new PooledMemoryStream();
        public Action<byte[], int, int> OnOut;
        int remaining = 64000;
        public void Push(PooledMemoryStream curr)
        {
            if (curr.Position32 >= 64000)
            {
                Flush();
                prev = curr;
                Flush();
            }
            else if (prev == null)
            {
                prev = curr;
            }
            else if (curr.Position32 + prev.Position32 < remaining)
            {
                stream.Write(prev.GetBuffer(), 0, (int)prev.Position32);
                remaining -= (int)prev.Position32;
                prev = curr;
            }
            else
            {
                Flush();
                prev = curr;
                Flush();
            }
        }

        public void Flush()
        {
            if (stream.Position32 > 0)
            {
                OnOut(stream.GetBuffer(), 0, stream.Position32);
                stream.Position32 = 0;
                remaining = 64000;
            }
            if (prev != null)
            {
                OnOut(prev.GetBuffer(), 0, prev.Position32);
                prev = null;
            }
        }
    }
}
