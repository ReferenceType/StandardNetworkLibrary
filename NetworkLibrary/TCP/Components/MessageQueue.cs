using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace CustomNetworkLib
{
    internal class MessageQueue
    {
        public delegate void PrefixDelegate(ref byte[] buffer, int offset, int messageLenght);

        internal ConcurrentQueue<byte[]> SendQueue = new ConcurrentQueue<byte[]>();

        internal bool isThereAFragmentedMessage = false;
        internal bool isPrefixWritten = false;
        internal byte[] fragmentedMsg;
        internal int fragmentedMsgOffset = 0;
        internal int fragmentedMsgCount = 0;

        private int prefixLenght = 0;
        private int MaxIndexedMemory = 1280000;
        private int currentIndexedMemory = 0;
        private PrefixDelegate HowToFillPrefix;

        public MessageQueue(int maxIndexedMemory, int prefixLenght, PrefixDelegate howToFillPrefix)
        {
            MaxIndexedMemory = maxIndexedMemory;
            this.prefixLenght = prefixLenght;
            HowToFillPrefix = howToFillPrefix;
        }

        // Enqueue if you are within the memory limits
        public bool TryEnqueueMessage(byte[] bytes)
        { 
            if (Volatile.Read(ref currentIndexedMemory) < MaxIndexedMemory)
            {
                Interlocked.Add(ref currentIndexedMemory, bytes.Length + prefixLenght);
                SendQueue.Enqueue(bytes);
                return true;
            }
            return false;
        }

        public bool TryFlushQueue(ref byte[] buffer, int offset, out int amountWritten)
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
                if (prefixLenght + bytes.Length > buffer.Length - offset)
                {
                    // save the message, we already dequeued it
                    fragmentedMsg = bytes;
                    isThereAFragmentedMessage = true;

                    int available = buffer.Length - offset;
                    // if i cant write prefix, it will be written on next flush.
                    if (available < prefixLenght)
                        break;

                    Interlocked.Add(ref currentIndexedMemory, -available);

                    WriteMessagePrefix(ref buffer, offset, bytes.Length);

                    isPrefixWritten = true;

                    offset += prefixLenght;
                    available -= prefixLenght;

                    // Fill all available space
                    Buffer.BlockCopy(fragmentedMsg, fragmentedMsgOffset, buffer, offset, available);
                    amountWritten = buffer.Length;

                    fragmentedMsgOffset += available;
                    break;
                }
                // Simply copy the message to buffer.
                Interlocked.Add(ref currentIndexedMemory, -(prefixLenght + bytes.Length));

                WriteMessagePrefix(ref buffer, offset, bytes.Length);
                offset += prefixLenght;

                Buffer.BlockCopy(bytes, 0, buffer, offset, bytes.Length);
                offset += bytes.Length;
                amountWritten += prefixLenght + bytes.Length;

            }
            return amountWritten != 0;

        }
        private void CheckLeftoverMessage(ref byte[] buffer, ref int bufferOffset, ref int amountWritten)
        {
            if (isThereAFragmentedMessage)
            {
                int availableInBuffer = buffer.Length - bufferOffset;
                fragmentedMsgCount = fragmentedMsg.Length - fragmentedMsgOffset;
                if (!isPrefixWritten)
                {
                    WriteMessagePrefix(ref buffer, bufferOffset, fragmentedMsg.Length);
                    isPrefixWritten = true;

                    bufferOffset += prefixLenght;
                    availableInBuffer -= prefixLenght;
                    amountWritten += prefixLenght;
                }
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
                    isPrefixWritten = false;
                    isThereAFragmentedMessage = false;
                }
                // Wont fit, copy partially until you fill the buffer
                else
                {
                    System.Buffer.BlockCopy(fragmentedMsg, fragmentedMsgOffset, buffer, bufferOffset, availableInBuffer);
                    amountWritten += availableInBuffer;

                    fragmentedMsgOffset += availableInBuffer;
                    Interlocked.Add(ref currentIndexedMemory, - amountWritten);

                }
            }
        }

        
        public bool IsEmpty()
        {
            return SendQueue.IsEmpty;
        }

        protected void WriteMessagePrefix(ref byte[] buffer, int offset, int messageLength)
        {
            HowToFillPrefix?.Invoke(ref buffer, offset, messageLength);

        }


    }
}
