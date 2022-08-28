using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace CustomNetworkLib
{

    public class IOBuffer
    {
        public byte[] buffer;
        public byte[] Bigdata;
        public int available;
        public int offset = 0;
        private readonly object locker = new object();

        public IOBuffer(int capacity)
        {
            buffer = new byte[capacity];
            available = capacity;
        }
        public void AddBytes(byte[] bytes) 
        {
            lock (locker)
            {
            Interlocked.Add(ref available, -bytes.Length);

            Buffer.BlockCopy(bytes, 0, buffer, offset, bytes.Length);
            Interlocked.Add(ref offset, bytes.Length);

            // available -= bytes.Length;
            //offset += bytes.Length;
            }

        }

        public void StoreBigData(byte[] bytes)
        {
            Bigdata = bytes;
        }

        public void Reset()
        {
            lock (locker)
            {
                //Interlocked.Exchange(ref available, buffer.Length);
                available = buffer.Length;
                offset = 0;
                Bigdata = null;
            }
           
        }

        public void Swap(ref byte[] buffer)
        {
            lock (locker)
            {
                Interlocked.Exchange(ref buffer, this.buffer);
                Reset();
            }
            
        }
        
        public int GetAvailable() { return Volatile.Read(ref available); }
    }
}
