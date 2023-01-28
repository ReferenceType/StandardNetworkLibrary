using NetworkLibrary;
using NetworkLibrary.Components;
using NetworkLibrary.TCP.Base;
using Protobuff.Components.TransportWrapper.SecureProtoTcp;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace Protobuff.Components.ProtoTcp
{
    internal class ProtoSessionInternal : TcpSession, IProtoSession
    {
        ProtoMessageQueue mq;
        private ByteMessageReader reader;
        public ProtoSessionInternal(SocketAsyncEventArgs acceptedArg, Guid sessionId) : base(acceptedArg, sessionId)
        {
            reader = new ByteMessageReader(sessionId);
            reader.OnMessageReady += HandleMessage;
        }

        protected override IMessageQueue CreateMessageQueue()
        {
            mq = new ProtoMessageQueue(MaxIndexedMemory, true);
            return mq;
        }
        private void HandleMessage(byte[] arg1, int arg2, int arg3)
        {
            base.HandleReceived(arg1, arg2, arg3);
        }
        protected override void HandleReceived(byte[] buffer, int offset, int count)
        {
            reader.ParseBytes(buffer, offset, count);
        }
        //protected override void ConfigureBuffers()
        //{
        //    recieveBuffer = BufferPool.RentBuffer(socketRecieveBufferSize);
        //}
        public void SendAsync(MessageEnvelope message)
        {
            enqueueLock.Take();
            if (IsSessionClosing())
            {
                enqueueLock.Release();
                return;
            }

            if (SendSemaphore.IsTaken())
            {
                if (mq.TryEnqueueMessage(message))
                {
                    enqueueLock.Release();
                    return;
                }
                // At this point queue is saturated, shall we drop?
                else if (DropOnCongestion)
                {
                    enqueueLock.Release();
                    return;
                }
                // Otherwise we will wait semaphore
            }

            SendSemaphore.Take();
            if (IsSessionClosing())
            {
                SendSemaphore.Release();
                return;
            }

            // you have to push it to queue because queue also does the processing.
            mq.TryEnqueueMessage(message);
            mq.TryFlushQueue(ref sendBuffer, 0, out int amountWritten);
            FlushSendBuffer(0,amountWritten);
            enqueueLock.Release();

        }
        public void SendAsync<T>(MessageEnvelope envelope, T message) where T : IProtoMessage
        {
            enqueueLock.Take();
            if (IsSessionClosing())
            {
                enqueueLock.Release();
                return;
            }

            if (SendSemaphore.IsTaken())
            {
                if (mq.TryEnqueueMessage(envelope, message))
                {
                    enqueueLock.Release();
                    return;
                }
                // At this point queue is saturated, shall we drop?
                else if (DropOnCongestion)
                {
                    enqueueLock.Release();
                    return;
                }
                // Otherwise we will wait semaphore
            }

            SendSemaphore.Take();
            if (IsSessionClosing())
            {
                SendSemaphore.Release();
                return;
            }

            // you have to push it to queue because queue also does the processing.
            mq.TryEnqueueMessage(envelope, message);
            mq.TryFlushQueue(ref sendBuffer, 0, out int amountWritten);
            FlushSendBuffer(0,amountWritten);
            enqueueLock.Release();

        }
    }
}
