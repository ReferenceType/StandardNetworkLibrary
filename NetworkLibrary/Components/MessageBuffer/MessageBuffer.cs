using System;
using System.Threading;

namespace NetworkLibrary.Components.MessageBuffer
{
    public class MessageBuffer : IMessageQueue
    {
        public int CurrentIndexedMemory { get => Volatile.Read(ref currentIndexedMemory); }
        public int MaxIndexedMemory;
        public long TotalMessageDispatched { get; protected set; }

        protected PooledMemoryStream writeStream = new PooledMemoryStream();
        protected PooledMemoryStream flushStream = new PooledMemoryStream();
        protected readonly object loki = new object();
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
            lock (loki)
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
            lock (loki)
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
            lock (loki)
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

        protected virtual void Dispose(bool disposing)
        {
            lock (loki)
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
