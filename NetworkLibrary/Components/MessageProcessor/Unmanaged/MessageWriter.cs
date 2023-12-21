using System;
using System.Runtime.CompilerServices;

namespace NetworkLibrary.Components
{
    internal class MessageWriter
        : IMessageProcessor
    {
        public byte[] bufferInternal { get; private set; }
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
                // write whatever you can
                _ = Flush();
                IsHoldingMessage = true;
                return false;

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool Flush()
        {

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
                IsHoldingMessage = false;
                pendingRemaining = 0;
                pendingMessageOffset = 0;
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

                count += (bufferInternal.Length - offset);

                pendingMessageOffset += (bufferInternal.Length - offset);
                pendingRemaining -= (bufferInternal.Length - offset);
                return false;
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetBuffer(out byte[] Buffer, out int offset, out int count)
        {
            Buffer = this.bufferInternal;
            offset = origialOffset;
            count = this.count;
        }

        public void Dispose()
        {
            //bufferInternal= null;
        }
    }
}

