using NetworkLibrary;
using NetworkLibrary.Components;
using NetworkLibrary.TCP.Base;
using NetworkLibrary.TCP.SSL.Base;
using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Text;

namespace Protobuff.Components.TransportWrapper.SecureProtoTcp
{
    internal interface IProtoSession : IAsyncSession
    {
        void SendAsync(MessageEnvelope message);
        void SendAsync<T>(MessageEnvelope envelope, T message) where T : IProtoMessage;
    }
    internal class SecureProtoSessionInternal : SslSession, IProtoSession
    {
        ProtoMessageQueue mq;
        private ByteMessageReader reader;

        public SecureProtoSessionInternal(Guid sessionId, SslStream sessionStream) : base(sessionId, sessionStream)
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
        protected override void ConfigureBuffers()
        {
            receiveBuffer = BufferPool.RentBuffer(ReceiveBufferSize);
        }
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
            WriteOnSessionStream(amountWritten);
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
            WriteOnSessionStream(amountWritten);
            enqueueLock.Release();

        }

    }
}
