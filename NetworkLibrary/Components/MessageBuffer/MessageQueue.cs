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
    internal sealed class MessageQueue<T>: IMessageProcessQueue where T : IMessageProcessor
    {
        internal ConcurrentQueue<byte[]> SendQueue = new ConcurrentQueue<byte[]>();
        int MaxIndexedMemory;
        int currentIndexedMemory = 0;
        private T processor;
        private bool flush;
        private long totalMessageFlushed;
        public int CurrentIndexedMemory => currentIndexedMemory;

        public long TotalMessageDispatched => totalMessageFlushed;

        //private byte[] heldOver;
        //private int heldoverOffset;
        //private int heldoverRemaining;
        //bool heldoverHeaderWritten;


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
            if (flush)
            {
                if (!processor.Flush())
                {
                    processor.GetBuffer(out buffer, out _, out amountWritten);
                    return true;
                }
                flush = false;
            }
            int memcount = 0;
            while (SendQueue.TryDequeue(out byte[] bytes))
            {
                totalMessageFlushed++;

                memcount += bytes.Length;
                if (!processor.ProcessMessage(bytes))
                {
                    flush = true;
                    break;
                };
                bytes = null;
               
            }
            Interlocked.Add(ref currentIndexedMemory, -memcount);
            processor.GetBuffer(out buffer, out _, out amountWritten);
            return amountWritten != 0;

        }
        #region Commented to  be returned 
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]

        //public bool TryFlushQueue_(ref byte[] buffer, int offset, out int amountWritten)
        //{
        //    amountWritten = 0;
        //    CheckForHeldoverMsg(ref buffer, ref offset, ref amountWritten);
        //    if (buffer.Length - offset <= 4)
        //    {
        //        return true;
        //    }
        //    int availableSpace = buffer.Length -offset;
        //    while (SendQueue.TryDequeue(out byte[] bytes))
        //    {
        //        Interlocked.Add(ref currentIndexedMemory, -bytes.Length);

        //        if (availableSpace>4)
        //        {
        //            PrefixWriter.WriteInt32AsBytes(ref buffer, offset, bytes.Length);
        //            amountWritten += 4;
        //            offset += 4;
        //            availableSpace -= 4;
        //        }
        //        else
        //        {
        //            heldOver = bytes;
        //            heldoverOffset = 0;
        //            heldoverRemaining=heldOver.Length;
        //            heldoverHeaderWritten = false;
        //            return true;
        //        }

        //        if(availableSpace >= bytes.Length)
        //        {
        //            Buffer.BlockCopy(bytes, 0, buffer, offset,bytes.Length);
        //            offset+=bytes.Length;
        //            amountWritten+=bytes.Length;
        //            availableSpace -= bytes.Length;
        //        }
        //        else
        //        {
        //            heldOver = bytes;
        //            Buffer.BlockCopy(bytes, 0,buffer, offset, availableSpace);
        //            amountWritten+=availableSpace;
        //            heldoverOffset += availableSpace;
        //            heldoverRemaining = heldOver.Length - availableSpace;
        //            heldoverHeaderWritten = true;

        //            return true;
        //        }


        //    }
        //    return amountWritten != 0;


        //}

        //private void CheckForHeldoverMsg(ref byte[] buffer, ref int offset, ref int amountWritten)
        //{
        //    if (heldOver != null)
        //    {
        //        if (!heldoverHeaderWritten)
        //        {
        //            PrefixWriter.WriteInt32AsBytes(ref buffer, offset, heldOver.Length);
        //            offset+=4;
        //            amountWritten += 4;
        //            heldoverHeaderWritten = true;
        //        }
        //        if (buffer.Length - offset > heldoverRemaining)
        //        {
        //            Buffer.BlockCopy(heldOver, heldoverOffset, buffer, offset, heldoverRemaining);
        //            offset += heldoverRemaining;
        //            amountWritten+=heldoverRemaining;

        //            heldoverOffset=0;
        //            heldoverRemaining = 0;
        //            heldOver = null;
        //        }
        //        else
        //        {
        //            Buffer.BlockCopy(heldOver, heldoverOffset, buffer, offset, buffer.Length-offset);
        //            amountWritten= buffer.Length-offset;

        //            heldoverOffset += buffer.Length-offset;
        //            heldoverRemaining = heldOver.Length - heldoverOffset;
        //            offset = buffer.Length;
        //        }
        //    }
        //}
        #endregion
        public bool IsEmpty()
        {
            return SendQueue.IsEmpty && !processor.IsHoldingMessage;
        }

        public bool TryEnqueueMessage(byte[] bytes, int offset, int count)
        {
            var array = ByteCopy.ToArray(bytes, offset, count);
            return TryEnqueueMessage(array);
        }
    }
}
