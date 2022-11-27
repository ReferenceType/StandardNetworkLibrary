using NetworkLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace NetworkLibrary.Components
{
    internal sealed class DelimitedMessageWriter : IMessageProcessor
    {
        private byte[] bufferInternal;
        private int offset;
        private int origialOffset;
        public int count;

        public bool IsHoldingMessage { get; private set; }

        byte[] pendingMessage;
        int pendingMessageOffset;
        int pendingRemaining;
        private bool writeHeaderOnflush;

        public void SetBuffer(ref byte[] buffer, int offset)
        {
            bufferInternal = buffer;
            this.offset = offset;
            origialOffset = offset;
            count = 0;

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ProcessMessage(byte[] message)
        {
            if (IsHoldingMessage)
                throw new InvalidOperationException("You can not process new message before heldover message is fully flushed");
            else if (bufferInternal.Length - offset >= 5)
            {
                if (BitConverter.IsLittleEndian)
                {
                    bufferInternal[offset] = (byte)message.Length;
                    bufferInternal[offset + 1] = (byte)(message.Length >> 8);
                    bufferInternal[offset + 2] = (byte)(message.Length >> 16);
                    bufferInternal[offset + 3] = (byte)(message.Length >> 24);
                }
                else
                {
                    bufferInternal[offset+3] = (byte)message.Length;
                    bufferInternal[offset + 2] = (byte)(message.Length >> 8);
                    bufferInternal[offset + 1] = (byte)(message.Length >> 16);
                    bufferInternal[offset] = (byte)(message.Length >> 24);
                }
               
                offset += 4;
                count += 4;
            }
            else
            {
                writeHeaderOnflush = true;
                pendingMessage = message;
                pendingMessageOffset = 0;
                pendingRemaining = pendingMessage.Length;
                IsHoldingMessage = true;
                return false;

            }

            if (bufferInternal.Length - offset >= message.Length)
            {
                System.Buffer.BlockCopy(message, 0, bufferInternal, offset, message.Length);
                offset += message.Length;
                count += message.Length;
                return true;
            }
            else
            {
                pendingMessage = message;
                pendingMessageOffset = 0;
                pendingRemaining = pendingMessage.Length;
                _ = Flush();
                IsHoldingMessage = true;
                return false;

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Flush()
        {
            if (writeHeaderOnflush)
            {
                writeHeaderOnflush = false;
                if (BitConverter.IsLittleEndian)
                {
                    bufferInternal[offset] = (byte)pendingMessage.Length;
                    bufferInternal[offset + 1] = (byte)(pendingMessage.Length >> 8);
                    bufferInternal[offset + 2] = (byte)(pendingMessage.Length >> 16);
                    bufferInternal[offset + 3] = (byte)(pendingMessage.Length >> 24);
                }
                else
                {
                    bufferInternal[offset + 3] = (byte)pendingMessage.Length;
                    bufferInternal[offset + 2] = (byte)(pendingMessage.Length >> 8);
                    bufferInternal[offset + 1] = (byte)(pendingMessage.Length >> 16);
                    bufferInternal[offset] = (byte)(pendingMessage.Length >> 24);
                }
                offset += 4;
                count += 4;

            }
            if (pendingRemaining <= bufferInternal.Length - offset)
            {
                System.Buffer.BlockCopy(pendingMessage, pendingMessageOffset, bufferInternal, offset, pendingRemaining);
                offset += pendingRemaining;
                count += pendingRemaining;

                pendingMessage = null;
                pendingRemaining = 0;
                pendingMessageOffset=0;
                IsHoldingMessage = false;
                return true;
            }
            else
            {
                System.Buffer.BlockCopy(pendingMessage, pendingMessageOffset, bufferInternal, offset, bufferInternal.Length - offset);
                count += (bufferInternal.Length - offset);

                pendingMessageOffset += (bufferInternal.Length - offset);
                pendingRemaining -= (bufferInternal.Length - offset);
                return false;
            }
        }

        public void GetBuffer(out byte[] Buffer, out int offset, out int count)
        {
            Buffer = this.bufferInternal;
            offset = origialOffset;
            count = this.count;
        }

        public void Dispose()
        {
           bufferInternal= null;
        }
    }
}
