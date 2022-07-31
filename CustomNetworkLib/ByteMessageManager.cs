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
        public byte[] header = new byte[HeaderLenght];

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
        // get as ref? nah
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
        public readonly Guid Guid;
        public Action<Guid, byte[]> OnMessageReady;

        private enum OperationState
        {
            AwaitingMsgHeader,
            AwaitingMsgBody,
        }

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

        public void AppendBytes(byte[] bytes,int offset, int count)
        {
            HandleBytes(bytes,offset,count);
        }

        // try salvage get this bytes as ref with offset

        private void HandleBytes(byte[] incomingBytes, int offset, int count)
        {
            switch (currentState)
            {
                case OperationState.AwaitingMsgHeader:
                    if (count - offset >= currentExpectedByteLenght)
                    {
                        if (currentExpectedByteLenght > 4)
                        { }
                        for (int i = 0; i < currentExpectedByteLenght; i++)
                        {
                            messageBuffer.AppendHeaderByte(incomingBytes[i + offset]);
                        }

                        // perfect msg, since this is most common case, more opt.
                        if (count-offset - currentExpectedByteLenght == messageBuffer.ExpectedMsgLenght)
                        {
                            //messageBuffer.AppendMessageChunk(incomingBytes, currentExpectedByteLenght, incomingBytes.Length - currentExpectedByteLenght);
                            //MessageReady(messageBuffer);

                            byte[] msgBytes = new byte[messageBuffer.ExpectedMsgLenght];
                            Buffer.BlockCopy(incomingBytes, 4, msgBytes, 0, msgBytes.Length);

                            MessageReady(msgBytes);
                            messageBuffer.Reset();

                        }
                        // multiple msgs or partial incomplete msg.
                        else
                        {
                            //byte[] nextMsgs = new byte[incomingBytes.Length - currentExpectedByteLenght];
                            //Buffer.BlockCopy(incomingBytes, currentExpectedByteLenght, nextMsgs, 0, nextMsgs.Length);

                            currentState = OperationState.AwaitingMsgBody;
                            currentExpectedByteLenght = messageBuffer.ExpectedMsgLenght;
                            if (currentExpectedByteLenght == 0)
                            {

                            }
                            
                                HandleBytes(incomingBytes, 4 + offset, count);
                        }
                    }
                    else if (count - offset == 0)
                        return;
                    // Fragmented header.
                    else
                    {
                        messageBuffer.AppendHeaderChunk(incomingBytes, count);
                        currentExpectedByteLenght = MessageBuffer.HeaderLenght - count;
                    }
                    break;
                case OperationState.AwaitingMsgBody:
                    // overflown message
                    if (count-offset > currentExpectedByteLenght)
                    {
                        messageBuffer.AppendMessageChunk(incomingBytes, offset, currentExpectedByteLenght);
                        MessageReady(messageBuffer);
                        messageBuffer.Reset();
                        // optimise here
                        //byte[] NextMsgs = new byte[incomingBytes.Length - currentExpectedByteLenght];
                        //Buffer.BlockCopy(incomingBytes, currentExpectedByteLenght, NextMsgs, 0, NextMsgs.Length);
                        int offset_ = currentExpectedByteLenght;
                        currentExpectedByteLenght = MessageBuffer.HeaderLenght;
                        currentState = OperationState.AwaitingMsgHeader;
                        if(count - (offset + offset_)<0)
                            HandleBytes(incomingBytes, offset_ + offset, count);
                        else
                            HandleBytes(incomingBytes, offset_+offset, count );
                       
                    }
                    // incomplete msg
                    else if (count-offset < currentExpectedByteLenght)
                    {
                        messageBuffer.AppendMessageChunk(incomingBytes, offset, count-offset);
                        currentExpectedByteLenght = currentExpectedByteLenght - (count-offset);
                        if(currentExpectedByteLenght == 0)
                        {

                        }
                    }
                    // perfect size
                    else
                    {
                        messageBuffer.AppendMessageChunk(incomingBytes, offset, count-offset);

                        MessageReady(messageBuffer);
                        messageBuffer.Reset();

                        currentExpectedByteLenght = MessageBuffer.HeaderLenght;
                        currentState = OperationState.AwaitingMsgHeader;
                    }
                    break;
            }
           
        }


        private void MessageReady(MessageBuffer buffer)
        {
            var byteMsg = buffer.GetMessageBytes();
            MessageReady(byteMsg);
        }
        private void MessageReady(byte[] byteMsg)
        {
            OnMessageReady?.Invoke(Guid, byteMsg);
        }
    }
}
