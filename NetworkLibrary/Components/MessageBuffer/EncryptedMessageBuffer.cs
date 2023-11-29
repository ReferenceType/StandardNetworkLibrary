using System;
using System.Threading;

namespace NetworkLibrary.Components.MessageBuffer
{
    public class EncryptedMessageBuffer : IMessageQueue
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
        internal ConcurrentAesAlgorithm aesAlgorithm;
        public EncryptedMessageBuffer(int maxIndexedMemory,ConcurrentAesAlgorithm algorithm, bool writeLengthPrefix = true)
        {
            this.aesAlgorithm = algorithm;
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
                    writeStream.Reserve(bytes.Length + 256);
                    
                    int amount = aesAlgorithm.EncryptInto(bytes, 0, bytes.Length, writeStream.GetBuffer(), writeStream.Position32+4);

                    writeStream.WriteInt(amount);
                    writeStream.Position32 += amount;
                   
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

                    writeStream.Reserve(count + 256);

                    int amount = aesAlgorithm.EncryptInto(bytes, offset, count, writeStream.GetBuffer(), writeStream.Position32 + 4);

                    writeStream.WriteInt(amount);
                    writeStream.Position32 += amount;

                    currentIndexedMemory += count;
                    return true;
                }

            }
            return false;
        }
        public bool TryEnqueueMessage(byte[] data1, int offset1, int count1, byte[] data2, int offset2, int count2)
        {
            lock (loki)
            {
                if (currentIndexedMemory < MaxIndexedMemory && !disposedValue)
                {

                    TotalMessageDispatched++;

                    writeStream.Reserve(count1+ count2 + 256);

                    int amount = aesAlgorithm.EncryptInto(data1, offset1, count1, data2,offset2,count2, writeStream.GetBuffer(), writeStream.Position32 + 4);

                    writeStream.WriteInt(amount);
                    writeStream.Position32 += amount;

                    currentIndexedMemory += count2 + count1;
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
