using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;


namespace CustomNetworkLib
{
    internal class MessageBuffer
    {
        public const int HeaderLenght = 4;
        public byte[] buffer;
        public byte[] header { get; } = new byte[HeaderLenght];

        public int CurrentMsgBufferPos;
        public int CurrentHeaderBufferPos;
        public int ExpectedMsgLenght;
        private int originalCapacity;
       
        public MessageBuffer(int capacity, int currentWriteIndex = 0)
        {
            this.buffer = new byte[capacity];
            originalCapacity=capacity;
            CurrentMsgBufferPos = currentWriteIndex;
        }
      
        public void AppendMessageChunk(byte[] bytes,int offset, int count)
        {
            if (buffer.Length - CurrentMsgBufferPos < count)
                Extend(count);

            Buffer.BlockCopy(bytes, offset, buffer, CurrentMsgBufferPos, count);
            //for (int i = 0; i < count; i++)
            //{
            //    messageBuffer[i+CurrentHeaderBufferPos]=bytes[i + offset];
            //}
            CurrentMsgBufferPos += count;
        }

        private void Extend(int size)
        {
            Console.WriteLine("Alloc");

            Array.Resize(ref buffer, size + buffer.Length);
        }

        public void AppendHeaderChunk(byte[] headerPart,int offset,int count)
        {
            for (int i = 0; i < count; i++)
            {
                AppendHeaderByte(headerPart[i+offset]);
            }
        }
        public int AppendHeader(byte[]buffer,int offset)
        {
            return ExpectedMsgLenght=BitConverter.ToInt32(buffer, offset);
        }

        public void AppendHeaderByte(byte headerByte)
        {
            if (CurrentHeaderBufferPos == HeaderLenght)
            {
                throw new InvalidOperationException(String.Format("Header byte tried to be added while header was full." +
                    " Current heder bytes: {0}{1}{2}{3}", header[0], header[1], header[2], header[3])+"expectedMdg len = " + ExpectedMsgLenght);
            }
            header[CurrentHeaderBufferPos] = headerByte;
            CurrentHeaderBufferPos++;
            if (CurrentHeaderBufferPos == HeaderLenght)
            {
                ExpectedMsgLenght = BitConverter.ToInt32(header, 0);
                if (buffer.Length < ExpectedMsgLenght)
                {
                    //Console.WriteLine("Alloc by header");
                    buffer = new byte[ExpectedMsgLenght];
                }
            }
            
           
            
        }


        internal void Reset(bool v = true)
        {
           CurrentHeaderBufferPos = 0;
           CurrentMsgBufferPos= 0;
           ExpectedMsgLenght = 0;
            if (v&& buffer.Length > originalCapacity)
            {
                this.buffer = new byte[originalCapacity];
            }
        }
        internal void FreeMemory()
        {
            if (buffer.Length > originalCapacity)
            {
                this.buffer = new byte[originalCapacity];
                //Console.WriteLine("De Alloc by reset");
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
            if (count >= currentExpectedByteLenght)
            {
                if(messageBuffer.CurrentHeaderBufferPos!=0)
                    messageBuffer.AppendHeaderChunk(incomingBytes, offset,currentExpectedByteLenght);
                else
                    messageBuffer.AppendHeader(incomingBytes, offset);

                // perfect msg -- i do a hot path here
                if (count - currentExpectedByteLenght == messageBuffer.ExpectedMsgLenght)
                {
                    MessageReady(incomingBytes, currentExpectedByteLenght, messageBuffer.ExpectedMsgLenght);
                    messageBuffer.Reset();
                }
                // multiple msgs or partial incomplete msg.
                else
                {
                    offset += currentExpectedByteLenght;
                    count -= currentExpectedByteLenght;
                    currentState = OperationState.AwaitingMsgBody;

                    currentExpectedByteLenght = messageBuffer.ExpectedMsgLenght;
                    HandleBody(incomingBytes, offset, count);
                }
            }
            // Fragmented header. we will get on next call,
            else
            {
                messageBuffer.AppendHeaderChunk(incomingBytes,offset, count);
                currentExpectedByteLenght  -= count;
            }
           
        }

        // 0 or more bodies 
        private void HandleBody(byte[] incomingBytes, int offset, int count)
        {
            // overflown message, there is for sure the message inside
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
                    MessageReady(messageBuffer.buffer, 0, messageBuffer.CurrentMsgBufferPos);
                    // call with false if mem no concern.
                    messageBuffer.Reset();
                }

                offset += currentExpectedByteLenght;
                count -= currentExpectedByteLenght;

                // read byte frame and determine next msg.
                if (count >= MessageBuffer.HeaderLenght)
                {
                    messageBuffer.AppendHeader(incomingBytes,offset);
                    currentExpectedByteLenght = messageBuffer.ExpectedMsgLenght;

                    offset += MessageBuffer.HeaderLenght;
                    count -= MessageBuffer.HeaderLenght;
                }
                // incomplete byte frame, we need to store the bytes 
                else if(count!=0)
                {
                    messageBuffer.AppendHeaderChunk(incomingBytes, offset, count);
                    currentExpectedByteLenght = MessageBuffer.HeaderLenght - (count);

                    currentState = OperationState.AwaitingMsgHeader;
                    return;
                }
                // nothing to store
                else
                {
                    currentExpectedByteLenght = MessageBuffer.HeaderLenght;
                    currentState = OperationState.AwaitingMsgHeader;
                    return;
                }
            }

            // we got the header, but we have a partial msg.
            if (count > 0)
            {
                messageBuffer.AppendMessageChunk(incomingBytes, offset, count);
                currentExpectedByteLenght = currentExpectedByteLenght - count;
            }
           

            
           
        }
       
        private void MessageReady(byte[] byteMsg, int offset, int count)
        {
            
            OnMessageReady?.Invoke(byteMsg, offset, count);
        }
    }
}
