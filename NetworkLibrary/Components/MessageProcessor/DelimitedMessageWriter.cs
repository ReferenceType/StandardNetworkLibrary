using NetworkLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace NetworkLibrary.Components
{
    internal sealed class DelimitedMessageWriter : IMessageProcessor
    {
        public byte[] Buffer { get; private set; }
        private int offset;
        private int origialOffset;
        public int count;

        public bool IsHoldingMessage { get; private set; }

        byte[] pendingMessage;
        int pendingMessageOffset;
        int pendingRemaining;
        private bool writeHeaderOnflush;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetBuffer(ref byte[] buffer, int offset)
        {
            Buffer = buffer;
            this.offset = offset;
            origialOffset = offset;
            count = 0;

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ProcessMessage(byte[] message)
        {
            if (IsHoldingMessage)
                throw new InvalidOperationException("You can not process new message before heldover message is fully flushed");
            if (Buffer.Length - offset >= 36)
            {
                Buffer[offset] = (byte)message.Length;
                Buffer[offset + 1] = (byte)(message.Length >> 8);
                Buffer[offset + 2] = (byte)(message.Length >> 16);
                Buffer[offset + 3] = (byte)(message.Length >> 24);
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

            if (Buffer.Length - offset >= message.Length)
            {
                System.Buffer.BlockCopy(message, 0, Buffer, offset, message.Length);
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
                Buffer[offset] = (byte)pendingMessage.Length;
                Buffer[offset + 1] = (byte)(pendingMessage.Length >> 8);
                Buffer[offset + 2] = (byte)(pendingMessage.Length >> 16);
                Buffer[offset + 3] = (byte)(pendingMessage.Length >> 24);
                offset += 4;
                count += 4;

            }
            if (pendingRemaining <= Buffer.Length - offset)
            {
                System.Buffer.BlockCopy(pendingMessage, pendingMessageOffset, Buffer, offset, pendingRemaining);
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
                System.Buffer.BlockCopy(pendingMessage, pendingMessageOffset, Buffer, offset, Buffer.Length - offset);
                count += (Buffer.Length - offset);

                pendingMessageOffset += (Buffer.Length - offset);
                pendingRemaining -= (Buffer.Length - offset);
                return false;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]

        public void GetBuffer(out byte[] Buffer, out int offset, out int count)
        {
            Buffer = this.Buffer;
            offset = origialOffset;
            count = this.count;
        }
    }
}
