using System;
using System.Drawing;
using System.Threading;

namespace NetworkLibrary.Components.MessageBuffer
{
    public class MessageBuffer : IMessageQueue
    {
        public int CurrentIndexedMemory { get => Interlocked.CompareExchange(ref currentIndexedMemory,0,0); }
        public int MaxIndexedMemory;
        public long TotalMessageDispatched { get; protected set; }

        protected PooledMemoryStream writeStream = new PooledMemoryStream();
        protected PooledMemoryStream flushStream = new PooledMemoryStream();
        protected readonly object bufferMtex = new object();
        protected bool writeLengthPrefix;
        protected int currentIndexedMemory;
        protected bool disposedValue;

        public MessageBuffer(int maxIndexedMemory, bool writeLengthPrefix = true)
        {
            this.writeLengthPrefix = writeLengthPrefix;
            MaxIndexedMemory = maxIndexedMemory;
        }

        public bool IsEmpty()
        {
            return Volatile.Read(ref disposedValue) || writeStream.Position == 0;
        }

        public bool TryEnqueueMessage(byte[] bytes)
        {
            lock (bufferMtex)
            {
                if (currentIndexedMemory < MaxIndexedMemory && !disposedValue)
                {

                    TotalMessageDispatched++;

                    if (writeLengthPrefix)
                    {
                        currentIndexedMemory += 4;
                        writeStream.WriteInt(bytes.Length);
                    }

                    writeStream.Write(bytes, 0, bytes.Length);
                    currentIndexedMemory += bytes.Length;
                    return true;
                }

            }
            return false;

        }
        public bool TryEnqueueMessage(byte[] bytes, int offset, int count)
        {
            lock (bufferMtex)
            {
                if (currentIndexedMemory < MaxIndexedMemory && !disposedValue)
                {

                    TotalMessageDispatched++;

                    if (writeLengthPrefix)
                    {
                        currentIndexedMemory += 4;
                        writeStream.WriteInt(count);
                    }

                    writeStream.Write(bytes, offset, count);
                    currentIndexedMemory += count;
                    return true;
                }

            }
            return false;
        }
        public bool TryFlushQueue(ref byte[] buffer, int offset, out int amountWritten)
        {
            lock (bufferMtex)
            {
                if (IsEmpty())
                {
                    amountWritten = 0;
                    return false;
                }

                var temp = writeStream;
                writeStream = flushStream;
                flushStream = temp;

                buffer = flushStream.GetBuffer();
                amountWritten = flushStream.Position32;

                currentIndexedMemory -= amountWritten;
                flushStream.Position32 = 0;

                return true;
            }


        }
        public bool TryEnqueueMessage(byte[] data1, int offset1, int count1, byte[] data2, int offset2, int count2)
        {
            lock (bufferMtex)
            {
                if (currentIndexedMemory < MaxIndexedMemory && !disposedValue)
                {

                    TotalMessageDispatched++;

                    if (writeLengthPrefix)
                    {
                        currentIndexedMemory += 4;
                        writeStream.WriteInt(count1+count2);
                    }

                    writeStream.Write(data1, offset1, count1);
                    writeStream.Write(data2, offset2, count2);
                    currentIndexedMemory += count1+count2;
                    return true;
                }

            }
            return false;
        }
        protected virtual void Dispose(bool disposing)
        {
            lock (bufferMtex)
            {
                if (!disposedValue)
                {
                    Volatile.Write(ref disposedValue, true);
                    if (disposing)
                    {
                        writeStream.Clear();
                        flushStream.Clear();
                        writeStream.Dispose();
                        flushStream.Dispose();
                    }
                }
            }

        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public void Flush()
        {
            flushStream.Clear();
        }
    }
}
