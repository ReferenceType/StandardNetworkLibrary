﻿using System;
using System.Runtime.CompilerServices;

namespace NetworkLibrary.Components
{
    internal class PlainMessageWriter : IMessageProcessor
    {
        public byte[] Buffer { get; private set; }
        private int offset;
        private int origialOffset;
        public int count;

        public bool IsHoldingMessage { get; private set; }

        byte[] pendingMessage;
        int pendingMessageOffset;
        int pendingRemaining;

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
                // write whatever you can
                _ = Flush();
                IsHoldingMessage = true;
                return false;

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Flush()
        {

            if (pendingRemaining <= Buffer.Length - offset)
            {
                System.Buffer.BlockCopy(pendingMessage, pendingMessageOffset, Buffer, offset, pendingRemaining);
                offset += pendingRemaining;
                count += pendingRemaining;

                pendingMessage = null;
                IsHoldingMessage = false;
                pendingRemaining = 0;
                pendingMessageOffset = 0;
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

        public void Dispose()
        {
            Buffer = null;
        }
    }
}

