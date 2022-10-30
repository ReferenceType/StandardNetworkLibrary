using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace NetworkLibrary.Components.MessageBuffer
{
    internal class MessageBuffer:IMessageProcessQueue
    {
        private MemoryStream s1 =  new MemoryStream();
        private MemoryStream s2 = new MemoryStream();
        private int currentIndexedMemory;
        public int CurrentIndexedMemory { get => currentIndexedMemory; }
        public int MaxIndexedMemory;

        
        public long TotalMessageDispatched { get; private set; }
        private readonly object loki =  new object();
        private bool writeLengthPrefix;
        public MessageBuffer(int maxIndexedMemory, bool writeLengthPrefix = true)
        {
            this.writeLengthPrefix = writeLengthPrefix;
            MaxIndexedMemory = maxIndexedMemory;
        }

        public bool IsEmpty()
        {
            return s1.Position == 0;
        }

        public bool TryEnqueueMessage(byte[] bytes)
        {
            if (CurrentIndexedMemory < MaxIndexedMemory)
            {
                lock (loki)
                {
                    TotalMessageDispatched++;
                    if (writeLengthPrefix)
                    {
                        var len = BitConverter.GetBytes(bytes.Length);

                        Interlocked.Add(ref currentIndexedMemory, 4);
                        s1.Write(len, 0, 4);

                    }

                    s1.Write(bytes, 0, bytes.Length);
                    Interlocked.Add(ref currentIndexedMemory, bytes.Length);

                    return true;
                }
               
            }
            return false;

        }
        public bool TryEnqueueMessage(byte[] bytes, int offset, int count)
        {
            if (CurrentIndexedMemory < MaxIndexedMemory)
            {
                lock (loki)
                {
                    TotalMessageDispatched++;

                    if (writeLengthPrefix)
                    {
                        var len = BitConverter.GetBytes(count);

                        Interlocked.Add(ref currentIndexedMemory, 4);
                        s1.Write(len, 0, 4);

                    }

                    s1.Write(bytes, offset, count);
                    Interlocked.Add(ref currentIndexedMemory, count);
                    return true;
                }

            }
            return false;
        }
        public bool TryFlushQueue(ref byte[] buffer, int offset, out int amountWritten)
        {
            if (IsEmpty())
            {
                amountWritten = 0;
                return false;

            }

            lock (loki)
            {
                var st = s1;
                s1 = s2;
                s2 = st;
            }
            buffer = s2.GetBuffer();
            amountWritten = (int)s2.Position;
            Interlocked.Add(ref currentIndexedMemory, -amountWritten);
            s2.Position = 0;
            return true;
        }

       
    }
}
