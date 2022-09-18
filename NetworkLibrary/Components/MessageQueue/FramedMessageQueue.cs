using NetworkLibrary.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace NetworkLibrary.Components.MessageQueue
{
    internal class FramedMessageQueue : MessageQueue, IMessageQueue
    {
        internal bool isPrefixWritten = false;
        private int prefixLenght = 4;
        public FramedMessageQueue(int maxIndexedMemory) : base(maxIndexedMemory)
        {
            MaxIndexedMemory = maxIndexedMemory;
        }

        public override bool TryFlushQueue(ref byte[] buffer, int offset, out int amountWritten)
        {
            amountWritten = 0;
            // Is there any message from previous call we dequeed and not fully sent?
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
        protected override void CheckLeftoverMessage(ref byte[] buffer, ref int bufferOffset, ref int amountWritten)
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
                    Buffer.BlockCopy(fragmentedMsg, fragmentedMsgOffset, buffer, bufferOffset, availableInBuffer);
                    amountWritten += availableInBuffer;

                    fragmentedMsgOffset += availableInBuffer;
                    Interlocked.Add(ref currentIndexedMemory, -amountWritten);

                }
            }
        }

        protected void WriteMessagePrefix(ref byte[] buffer, int offset, int messageLength)
        {
            PrefixWriter.WriteInt32AsBytes(buffer, offset, messageLength);
        }
    }
}
