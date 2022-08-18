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
        private int originalCapacity;
       
        public MessageBuffer(int capacity, int currentWriteIndex = 0)
        {
            this.messageBuffer = new byte[capacity];
            originalCapacity=capacity;
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

        public void AppendHeaderChunk(byte[] headerPart,int offset,int count)
        {
            for (int i = 0; i < count; i++)
            {
                AppendHeaderByte(headerPart[i+offset]);
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
            if (messageBuffer.Length > originalCapacity)
            {
                this.messageBuffer = new byte[originalCapacity];
                GC.Collect();
            }
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
            if (count-offset >= currentExpectedByteLenght)
            {
                messageBuffer.AppendHeaderChunk(incomingBytes, 0,currentExpectedByteLenght);

                // perfect msg
                if (count - currentExpectedByteLenght == messageBuffer.ExpectedMsgLenght)
                {
                    MessageReady(incomingBytes, currentExpectedByteLenght, messageBuffer.ExpectedMsgLenght);
                    messageBuffer.Reset();
                }
                // multiple msgs or partial incomplete msg.
                else
                {
                    offset += currentExpectedByteLenght;
                    //count -= currentExpectedByteLenght;

                    currentState = OperationState.AwaitingMsgBody;
                    currentExpectedByteLenght = messageBuffer.ExpectedMsgLenght;
                    HandleBody(incomingBytes, offset, count);
                }
            }
            // Fragmented header. we will get on next call,
            else
            {
                messageBuffer.AppendHeaderChunk(incomingBytes,0, count);
                currentExpectedByteLenght = MessageBuffer.HeaderLenght - (count-offset);
            }
           
        }

        // 0 or more bodies 
        private void HandleBody(byte[] incomingBytes, int offset, int count)
        {
            // overflown message
            while (count-offset >= currentExpectedByteLenght)
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
                //count -= currentExpectedByteLenght;
                // can read byte frame.
                if (count -offset>= MessageBuffer.HeaderLenght)
                {
                    currentExpectedByteLenght = BufferManager.ReadByteFrame(incomingBytes, offset);
                    offset += MessageBuffer.HeaderLenght;
                    //count -= MessageBuffer.HeaderLenght;
                }
                //if (count < 4)
                //{ }
                // incomplete byte frame
                else
                {
                    messageBuffer.AppendHeaderChunk(incomingBytes, offset, count-offset);
                    currentState = OperationState.AwaitingMsgHeader;
                    currentExpectedByteLenght = MessageBuffer.HeaderLenght - (count-offset);

                    return;
                }
            }

            // incomplete msg,
            if (count-offset < currentExpectedByteLenght)
            {
                messageBuffer.AppendMessageChunk(incomingBytes, offset, count-offset);
                currentExpectedByteLenght = currentExpectedByteLenght - (count-offset);

            }
           
        }
        private void MessageReady(MessageBuffer buffer)
        {
            //var byteMsg = buffer.GetMessageBytes();
            //OnMessageReady?.Invoke(buffer.messageBuffer, 0, buffer.CurrentMsgBufferPos);
            MessageReady(buffer.messageBuffer, 0, buffer.CurrentMsgBufferPos);
        }
        private void MessageReady(byte[] byteMsg, int offset, int count)
        {
            if (count != 5) 
            { }
            OnMessageReady?.Invoke(byteMsg, offset, count);
        }
    }
}
