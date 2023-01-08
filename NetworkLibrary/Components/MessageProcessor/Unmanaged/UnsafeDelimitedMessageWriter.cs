using NetworkLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace NetworkLibrary.Components.MessageProcessor.Unmanaged
{
    internal sealed class UnsafeDelimitedMessageWriter : IMessageProcessor
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
        public unsafe bool ProcessMessage(byte[] message)
        {
            if (IsHoldingMessage)
                throw new InvalidOperationException("You can not process new message before heldover message is fully flushed");
            else if (bufferInternal.Length - offset >= 36)
            {
                fixed (byte* b = &bufferInternal[offset])
                    *(int*)b = message.Length;
                //bufferInternal[offset] = (byte)message.Length;
                //bufferInternal[offset + 1] = (byte)(message.Length >> 8);
                //bufferInternal[offset + 2] = (byte)(message.Length >> 16);
                //bufferInternal[offset + 3] = (byte)(message.Length >> 24);
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
                //System.Buffer.BlockCopy(message, 0, bufferInternal, offset, message.Length);
                fixed (byte* destination = &bufferInternal[offset])
                {
                    fixed (byte* message_ = message)
                        Buffer.MemoryCopy(message_, destination, message.Length, message.Length);
                }
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
        public unsafe bool Flush()
        {
            if (writeHeaderOnflush)
            {
                writeHeaderOnflush = false;
                fixed (byte* b = &bufferInternal[offset])
                    *(int*)b = pendingMessage.Length;
                offset += 4;
                count += 4;

            }
            if (pendingRemaining <= bufferInternal.Length - offset)
            {
                //System.Buffer.BlockCopy(pendingMessage, pendingMessageOffset, bufferInternal, offset, pendingRemaining);
                fixed (byte* destination = &bufferInternal[offset])
                {
                    fixed (byte* message_ = &pendingMessage[pendingMessageOffset])
                        Buffer.MemoryCopy(message_, destination, pendingRemaining, pendingRemaining);
                }
                offset += pendingRemaining;
                count += pendingRemaining;

                pendingMessage = null;
                pendingRemaining = 0;
                pendingMessageOffset = 0;
                IsHoldingMessage = false;
                return true;
            }
            else
            {
                //System.Buffer.BlockCopy(pendingMessage, pendingMessageOffset, bufferInternal, offset, bufferInternal.Length - offset);
                fixed (byte* destination = &bufferInternal[offset])
                {
                    fixed (byte* message_ = &pendingMessage[pendingMessageOffset])
                        Buffer.MemoryCopy(message_, destination, bufferInternal.Length - offset, bufferInternal.Length - offset);
                }
                count += bufferInternal.Length - offset;

                pendingMessageOffset += bufferInternal.Length - offset;
                pendingRemaining -= bufferInternal.Length - offset;
                return false;
            }
        }

        public void GetBuffer(out byte[] Buffer, out int offset, out int count)
        {
            Buffer = bufferInternal;
            offset = origialOffset;
            count = this.count;
        }

        public void Dispose()
        {
        }
    }
}
