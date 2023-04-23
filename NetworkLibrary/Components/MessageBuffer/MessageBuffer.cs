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
                if (Volatile.Read(ref currentIndexedMemory) < MaxIndexedMemory && !disposedValue)
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
            lock (loki)
            {
                if (Volatile.Read(ref currentIndexedMemory) < MaxIndexedMemory && !disposedValue)
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
                amountWritten = (int)flushStream.Position;

                Interlocked.Add(ref currentIndexedMemory, -amountWritten);
                flushStream.Position = 0;

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
                        writeStream.Flush();
                        flushStream.Flush();
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
            flushStream.Flush();
        }
    }
}
