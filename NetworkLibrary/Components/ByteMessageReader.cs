using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace NetworkLibrary.Components
{
    // statefully parse byte messages with 4 byte lenght header,
    // under any fragmentation condition
    public class ByteMessageReader
    {
        public const int HeaderLenght = 4;
        private byte[] internalBufer;
        private byte[] headerBuffer;
        private int currentMsgBufferPosition;
        private int currentHeaderBufferPosition;
        private int expectedMsgLenght;
        private int currentExpectedByteLenght;
        private int originalCapacity;

        public event Action<byte[], int, int> OnMessageReady;

        private bool awaitingHeader;

        public ByteMessageReader( int bufferSize = 256000)
        {
            awaitingHeader = true;
            currentExpectedByteLenght = 4;

            headerBuffer = new byte[HeaderLenght];
            originalCapacity = bufferSize;
            internalBufer = BufferPool.RentBuffer(BufferPool.MinBufferSize);

            currentMsgBufferPosition = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBytes(byte[] bytes, int offset, int count)
        {
            HandleBytes(bytes, offset, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void HandleBytes(byte[] incomingBytes, int offset, int count)
        {
            if (awaitingHeader)
            {
                HandleHeader(incomingBytes, offset, count);
            }
            else
            {
                HandleBody(incomingBytes, offset, count);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void HandleHeader(byte[] incomingBytes, int offset, int count)
        {
            if (count >= currentExpectedByteLenght)
            {
                if (currentHeaderBufferPosition != 0)
                    AppendHeaderChunk(incomingBytes, offset, currentExpectedByteLenght);
                else
                    AppendHeader(incomingBytes, offset);

                // perfect msg - a hot path here
                if (count - currentExpectedByteLenght == expectedMsgLenght)
                {
                    MessageReady(incomingBytes, currentExpectedByteLenght, expectedMsgLenght);
                    Reset();
                }
                // multiple msgs or partial incomplete msg.
                else
                {
                    offset += currentExpectedByteLenght;
                    count -= currentExpectedByteLenght;
                    awaitingHeader = false;

                    currentExpectedByteLenght = expectedMsgLenght;
                    HandleBody(incomingBytes, offset, count);
                }
            }
            // Fragmented header. we will get on next call,
            else
            {
                AppendHeaderChunk(incomingBytes, offset, count);
                currentExpectedByteLenght -= count;
            }

        }

        // 0 or more bodies 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void HandleBody(byte[] incomingBytes, int offset, int count)
        {
            int remaining = count;
            // overflown message, there is for sure the message inside
            while (remaining >= currentExpectedByteLenght)
            {
                // nothing from prev call
                if (currentMsgBufferPosition == 0)
                {
                    MessageReady(incomingBytes, offset, currentExpectedByteLenght);
                    Reset();
                }
                // we had partial msg letover
                else
                {
                    AppendMessageChunk(incomingBytes, offset, currentExpectedByteLenght);
                    MessageReady(internalBufer, 0, currentMsgBufferPosition);
                    // call with false if mem no concern.
                    Reset(true);
                }

                offset += currentExpectedByteLenght;
                remaining -= currentExpectedByteLenght;

                // read byte frame and determine next msg.
                if (remaining >= 4)
                {
                    expectedMsgLenght = BitConverter.ToInt32(incomingBytes, offset);
                    currentExpectedByteLenght = expectedMsgLenght;
                    offset += 4;
                    remaining -= 4;
                }
                // incomplete byte frame, we need to store the bytes 
                else if (remaining != 0)
                {
                    AppendHeaderChunk(incomingBytes, offset, remaining);
                    currentExpectedByteLenght = 4 - remaining;

                    awaitingHeader = true;
                    return;
                }
                // nothing to store
                else
                {
                    currentExpectedByteLenght = 4;
                    awaitingHeader = true;
                    return;
                }
            }

            if (internalBufer.Length < expectedMsgLenght)
            {
                BufferPool.ReturnBuffer(internalBufer);
                internalBufer = BufferPool.RentBuffer(expectedMsgLenght);
            }
            // we got the header, but we have a partial msg.
            if (remaining > 0)
            {
                AppendMessageChunk(incomingBytes, offset, remaining);
                currentExpectedByteLenght = currentExpectedByteLenght - remaining;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MessageReady(byte[] byteMsg, int offset, int count)
        {
            OnMessageReady?.Invoke(byteMsg, offset, count);
        }

        #region Helper
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AppendMessageChunk(byte[] bytes, int offset, int count)
        {
            //Buffer.BlockCopy(bytes, offset, internalBufer, currentMsgBufferPosition, count);

            unsafe
            {
                fixed (byte* destination = &internalBufer[currentMsgBufferPosition])
                {
                    fixed (byte* message_ = &bytes[offset])
                        Buffer.MemoryCopy(message_, destination, count, count);
                }
            }

            currentMsgBufferPosition += count;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AppendHeaderChunk(byte[] headerPart, int offset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                headerBuffer[currentHeaderBufferPosition++] = headerPart[i + offset];

            }
            if (currentHeaderBufferPosition == HeaderLenght)
            {
                expectedMsgLenght = BitConverter.ToInt32(headerBuffer, offset);
                if (internalBufer.Length < expectedMsgLenght)
                {
                    BufferPool.ReturnBuffer(internalBufer);
                    internalBufer = BufferPool.RentBuffer(expectedMsgLenght);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int AppendHeader(byte[] buffer, int offset)
        {
            expectedMsgLenght = BitConverter.ToInt32(buffer, offset);
            if (internalBufer.Length < expectedMsgLenght)
            {
                BufferPool.ReturnBuffer(internalBufer);
                internalBufer = BufferPool.RentBuffer(expectedMsgLenght);
            }
            return expectedMsgLenght;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Reset(bool freeMemory = false)
        {
            currentHeaderBufferPosition = 0;
            currentMsgBufferPosition = 0;
            expectedMsgLenght = 0;
            if (freeMemory && internalBufer.Length > originalCapacity * 2)
            {
                FreeMemory();
            }
        }

        private void FreeMemory()
        {
            BufferPool.ReturnBuffer(internalBufer);
            internalBufer = BufferPool.RentBuffer(originalCapacity);
        }
        
        public void ReleaseResources()
        {
            OnMessageReady = null;

            var b = Interlocked.Exchange(ref internalBufer, null);

            if (b != null) 
            { 
                BufferPool.ReturnBuffer(b);
            }
        }
        #endregion
    }
}
