using NetworkLibrary.Components;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using NetworkLibrary.Utils;
using System.Runtime.CompilerServices;

namespace NetworkLibrary.Components
{
    internal sealed class MessageQueue<T> : IMessageProcessQueue where T : IMessageProcessor
    {
        internal ConcurrentQueue<byte[]> SendQueue = new ConcurrentQueue<byte[]>();
        int MaxIndexedMemory;
        int currentIndexedMemory = 0;
        private T processor;
        private bool flushNext;
        private long totalMessageFlushed;
        public int CurrentIndexedMemory => currentIndexedMemory;

        public long TotalMessageDispatched => totalMessageFlushed;

        public MessageQueue(int maxIndexedMemory, T processor)
        {
            MaxIndexedMemory = maxIndexedMemory;
            this.processor = processor;


        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnqueueMessage(byte[] bytes)
        {
            if (Volatile.Read(ref currentIndexedMemory) < MaxIndexedMemory)
            {
                Interlocked.Add(ref currentIndexedMemory, bytes.Length);
                SendQueue.Enqueue(bytes);
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryFlushQueue(ref byte[] buffer, int offset, out int amountWritten)
        {
            amountWritten = 0;
            processor.SetBuffer(ref buffer, offset);
            if (flushNext)
            {
                if (!processor.Flush())
                {
                    processor.GetBuffer(out buffer, out _, out amountWritten);
                    return true;
                }
                flushNext = false;
            }
            int memcount = 0;
            while (SendQueue.TryDequeue(out byte[] bytes))
            {
                totalMessageFlushed++;

                memcount += bytes.Length;
                if (!processor.ProcessMessage(bytes))
                {
                    flushNext = true;
                    break;
                };
                bytes = null;

            }
            Interlocked.Add(ref currentIndexedMemory, -memcount);
            processor.GetBuffer(out buffer, out _, out amountWritten);
            return amountWritten != 0;

        }

        public bool IsEmpty()
        {
            return SendQueue.IsEmpty && !processor.IsHoldingMessage;
        }

        public bool TryEnqueueMessage(byte[] bytes, int offset, int count)
        {
            var array = ByteCopy.ToArray(bytes, offset, count);
            return TryEnqueueMessage(array);
        }

        public void Dispose()
        {
            processor.Dispose();
        }

        public void Flush(){}
    }
}
