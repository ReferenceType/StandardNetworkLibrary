using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CustomNetworkLib
{
    internal class MessageBuffer
    {
        public const int HeaderLenght = 4;
        public byte[] messageBuffer;
        public byte[] header { get; } = new byte[HeaderLenght];

        public int CurrentMsgBufferPos;
        public int CurrentHeaderBufferPos;
        public int ExpectedMsgLenght;

       
        public MessageBuffer(int capacity, int currentWriteIndex = 0)
        {
            this.messageBuffer = new byte[capacity];
            CurrentMsgBufferPos = currentWriteIndex;
        }
      
        public void AppendMessageChunk(byte[] bytes,int offset, int count)
        {
            if (messageBuffer.Length - CurrentMsgBufferPos <= count)
                Extend(count);

            Buffer.BlockCopy(bytes, offset, messageBuffer, CurrentMsgBufferPos, count);
            CurrentMsgBufferPos += count;
        }

        private void Extend(int size)
        {
            Array.Resize(ref messageBuffer, size + messageBuffer.Length);
        }

        public void AppendHeaderChunk(byte[] headerPart,int count)
        {
            for (int i = 0; i < count; i++)
            {
                AppendHeaderByte(headerPart[i]);
            }
        }

        public void AppendHeaderByte(byte headerByte)
        {
            header[CurrentHeaderBufferPos] = headerByte;
            CurrentHeaderBufferPos++;

            if (CurrentHeaderBufferPos == HeaderLenght)
            {
                ExpectedMsgLenght = BitConverter.ToInt32(header, 0);
                if (messageBuffer.Length < ExpectedMsgLenght)
                {
                    Extend(ExpectedMsgLenght-messageBuffer.Length);
                }
            }
        }

        public byte[] GetMessageBytes()
        {
            byte[] bytes = new byte[ExpectedMsgLenght];
            Buffer.BlockCopy(messageBuffer, 0,bytes, 0,ExpectedMsgLenght);
            return bytes;
        }

        internal void Reset()
        {
           CurrentHeaderBufferPos = 0;
           CurrentMsgBufferPos= 0;
           ExpectedMsgLenght = 0;
        }
    }
    internal class ByteMessageManager
    {
        private enum OperationState
        {
            AwaitingMsgHeader,
            AwaitingMsgBody,
        }

        public readonly Guid Guid;
        public Action<byte[],int,int> OnMessageReady;

        private MessageBuffer messageBuffer;
        private OperationState currentState;
        private int currentExpectedByteLenght;
        
        public ByteMessageManager(Guid guid, int bufferSize = 256000)
        {
            currentState = OperationState.AwaitingMsgHeader;
            currentExpectedByteLenght = MessageBuffer.HeaderLenght;
            Guid = guid;
            messageBuffer = new MessageBuffer(bufferSize);
        }

        public void ParseBytes(byte[] bytes,int offset, int count)
        {
            HandleBytes(bytes,offset,count);
        }


        private void HandleBytes(byte[] incomingBytes, int offset, int count)
        {
            switch (currentState)
            {
                case OperationState.AwaitingMsgHeader:
                    HandleHeader(incomingBytes, offset, count);
                    break;

                case OperationState.AwaitingMsgBody:
                    HandleBody(incomingBytes, offset, count);
                    break;
            }
           
        }

        private void HandleHeader(byte[] incomingBytes, int offset, int count)
        {
            if (count >= currentExpectedByteLenght)
            {
                messageBuffer.AppendHeaderChunk(incomingBytes, currentExpectedByteLenght);

                // perfect msg
                if (count - currentExpectedByteLenght == messageBuffer.ExpectedMsgLenght)
                {
                    MessageReady(incomingBytes, MessageBuffer.HeaderLenght, messageBuffer.ExpectedMsgLenght);
                    messageBuffer.Reset();
                }
                // multiple msgs or partial incomplete msg.
                else
                {
                    currentState = OperationState.AwaitingMsgBody;
                    currentExpectedByteLenght = messageBuffer.ExpectedMsgLenght;
                    offset += MessageBuffer.HeaderLenght;
                    count -= MessageBuffer.HeaderLenght;
                    HandleBody(incomingBytes, offset, count);
                }
            }
            // Fragmented header. we will get on next call,
            else
            {
                messageBuffer.AppendHeaderChunk(incomingBytes, count);
                currentExpectedByteLenght = MessageBuffer.HeaderLenght - count;
            }
           
        }

        // 0 or more bodies 
        private void HandleBody(byte[] incomingBytes, int offset, int count)
        {
            // overflown message
            while (count >= currentExpectedByteLenght)
            {
                // nothing from prev call
                if (messageBuffer.CurrentMsgBufferPos == 0)
                {
                    MessageReady(incomingBytes, offset, currentExpectedByteLenght);
                    messageBuffer.Reset();
                }
                // we had partial msg letover
                else
                {
                    messageBuffer.AppendMessageChunk(incomingBytes, offset, currentExpectedByteLenght);
                    MessageReady(messageBuffer);
                    messageBuffer.Reset();
                }

                offset += currentExpectedByteLenght;
                count -= currentExpectedByteLenght;
                // can read byte frame.
                if (count >= MessageBuffer.HeaderLenght)
                {
                    currentExpectedByteLenght = BufferManager.ReadByteFrame(incomingBytes, offset);
                    offset += MessageBuffer.HeaderLenght;
                    count -= MessageBuffer.HeaderLenght;
                }
                // incomplete byte frame
                else
                {
                    messageBuffer.AppendMessageChunk(incomingBytes, offset, count);
                    currentState = OperationState.AwaitingMsgHeader;
                    currentExpectedByteLenght = MessageBuffer.HeaderLenght - count;

                    return;
                }
            }

            // incomplete msg,
            if (count < currentExpectedByteLenght)
            {
                messageBuffer.AppendMessageChunk(incomingBytes, offset, count);
                currentExpectedByteLenght = currentExpectedByteLenght - count;

            }
            //// perfect size
            //else
            //{
            //    MessageReady(incomingBytes, offset, count);
            //    messageBuffer.Reset();

            //    currentExpectedByteLenght = MessageBuffer.HeaderLenght;
            //    currentState = OperationState.AwaitingMsgHeader;
            //}
        }
        private void MessageReady(MessageBuffer buffer)
        {
            //var byteMsg = buffer.GetMessageBytes();
            OnMessageReady?.Invoke(buffer.messageBuffer, 0, buffer.CurrentMsgBufferPos);
        }
        private void MessageReady(byte[] byteMsg, int offset,int count)
        {
            OnMessageReady?.Invoke(byteMsg, offset, count);
        }
    }
}
