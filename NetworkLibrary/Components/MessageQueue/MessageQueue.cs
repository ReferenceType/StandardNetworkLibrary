using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace NetworkLibrary.Components.MessageQueue
{
    internal class MessageQueue : IMessageQueue
    {

        internal ConcurrentQueue<byte[]> SendQueue = new ConcurrentQueue<byte[]>();

        internal bool isThereAFragmentedMessage = false;
        internal byte[] fragmentedMsg;
        internal int fragmentedMsgOffset = 0;
        internal int fragmentedMsgCount = 0;

        protected int MaxIndexedMemory;
        protected int currentIndexedMemory = 0;

        public MessageQueue(int maxIndexedMemory)
        {
            MaxIndexedMemory = maxIndexedMemory;
        }

        // Enqueue if you are within the memory limits
        public virtual bool TryEnqueueMessage(byte[] bytes)
        {
            if (Volatile.Read(ref currentIndexedMemory) < MaxIndexedMemory)
            {
                Interlocked.Add(ref currentIndexedMemory, bytes.Length);
                SendQueue.Enqueue(bytes);
                return true;
            }
            return false;
        }

        public virtual bool TryFlushQueue(ref byte[] buffer, int offset, out int amountWritten)
        {
            amountWritten = 0;
            // Is there any message from previous call we dequeud and not fully sent?
            // If so, count will be more than 0.
            CheckLeftoverMessage(ref buffer, ref offset, ref amountWritten);
            if (amountWritten == buffer.Length)
                return true;

            while (SendQueue.TryDequeue(out byte[] bytes))
            {
                // Buffer cant carry entire message not enough space
                if (bytes.Length > buffer.Length - offset)
                {
                    // save the message, we already dequeued it
                    fragmentedMsg = bytes;
                    isThereAFragmentedMessage = true;

                    int available = buffer.Length - offset;
                    // if i cant write prefix, it will be written on next flush.

                    Interlocked.Add(ref currentIndexedMemory, -available);
                    // Fill all available space
                    Buffer.BlockCopy(fragmentedMsg, fragmentedMsgOffset, buffer, offset, available);
                    amountWritten = buffer.Length;

                    fragmentedMsgOffset += available;
                    break;
                }
                // Simply copy the message to buffer.
                Interlocked.Add(ref currentIndexedMemory, -bytes.Length);
                Buffer.BlockCopy(bytes, 0, buffer, offset, bytes.Length);

                offset += bytes.Length;
                amountWritten += bytes.Length;

            }
            return amountWritten != 0;

        }
        protected virtual void CheckLeftoverMessage(ref byte[] buffer, ref int bufferOffset, ref int amountWritten)
        {
            if (isThereAFragmentedMessage)
            {
                int availableInBuffer = buffer.Length - bufferOffset;
                fragmentedMsgCount = fragmentedMsg.Length - fragmentedMsgOffset;

                // Whats left will fit the buffer.
                if (fragmentedMsgCount <= availableInBuffer)
                {
                    Buffer.BlockCopy(fragmentedMsg, fragmentedMsgOffset, buffer, bufferOffset, fragmentedMsgCount);

                    bufferOffset += fragmentedMsgCount;
                    amountWritten += fragmentedMsgCount;
                    Interlocked.Add(ref currentIndexedMemory, -amountWritten);

                    // Reset
                    fragmentedMsgOffset = 0;
                    fragmentedMsg = null;
                    isThereAFragmentedMessage = false;
                }
                // Wont fit, copy partially until you fill the buffer
                else
                {
                    Buffer.BlockCopy(fragmentedMsg, fragmentedMsgOffset, buffer, bufferOffset, availableInBuffer);
                    amountWritten += availableInBuffer;

                    fragmentedMsgOffset += availableInBuffer;
                    Interlocked.Add(ref currentIndexedMemory, -amountWritten);

                }
            }
        }


        public virtual bool IsEmpty()
        {
            return SendQueue.IsEmpty;
        }




    }
}
