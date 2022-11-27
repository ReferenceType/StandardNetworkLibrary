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
        private int currentIndexedMemory;

        public int CurrentIndexedMemory { get => currentIndexedMemory; }
        public int MaxIndexedMemory;

        public long TotalMessageDispatched { get; private set; }

        private PooledMemoryStream writeStream = new PooledMemoryStream();
        private PooledMemoryStream flushStream = new PooledMemoryStream();
        private readonly object loki =  new object();
        private bool writeLengthPrefix;
        private bool disposedValue;

        public MessageBuffer(int maxIndexedMemory, bool writeLengthPrefix = true)
        {
            this.writeLengthPrefix = writeLengthPrefix;
            MaxIndexedMemory = maxIndexedMemory;
        }

        public bool IsEmpty()
        {
          
                return writeStream.Position == 0;

            
        }

        public bool TryEnqueueMessage(byte[] bytes)
        {
            if (Volatile.Read(ref currentIndexedMemory) < MaxIndexedMemory)
            {
                lock (loki)
                {
                    TotalMessageDispatched++;
                    if (writeLengthPrefix)
                    {
                        var len = BitConverter.GetBytes(bytes.Length);
                        Interlocked.Add(ref currentIndexedMemory, 4);
                        writeStream.Write(len, 0, 4);

                    }

                    writeStream.Write(bytes, 0, bytes.Length);
                    Interlocked.Add(ref currentIndexedMemory, bytes.Length);

                    return true;
                }
               
            }
            return false;

        }
        public bool TryEnqueueMessage(byte[] bytes, int offset, int count)
        {
            if (Volatile.Read(ref currentIndexedMemory) < MaxIndexedMemory)
            {
                lock (loki)
                {
                    TotalMessageDispatched++;

                    if (writeLengthPrefix)
                    {
                        var len = BitConverter.GetBytes(count);

                        Interlocked.Add(ref currentIndexedMemory, 4);
                        writeStream.Write(len, 0, 4);

                    }

                    writeStream.Write(bytes, offset, count);
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
                var temp = writeStream;
                writeStream = flushStream;
                flushStream = temp;

                buffer = flushStream.GetBuffer();
                amountWritten = (int)flushStream.Position;

                Interlocked.Add(ref currentIndexedMemory, -amountWritten);
                flushStream.Position = 0;

                return true;
            }

            
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    writeStream.Dispose();
                    flushStream.Dispose();
                }
                disposedValue = true;
            }
        }

       

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public void Flush()
        {
            flushStream.Flush();   
        }
    }
}
