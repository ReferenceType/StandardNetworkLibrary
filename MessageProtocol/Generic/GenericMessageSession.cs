using System;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace Protobuff.Components.TransportWrapper.Generic
{
    internal class GenericMessageSession : TcpSession
    {
        protected ISerialisableMessageQueue<IMessageEnvelope> mq;
        private IByteMessageReader reader;
        internal GenericMessageSession(SocketAsyncEventArgs acceptedArg, Guid sessionId) : base(acceptedArg, sessionId)
        {
            reader = new ByteMessageReader(sessionId);
            reader.OnMessageReady += HandleMessage;
            UseQueue = false;
        }

        // Override this.
        protected virtual ISerialisableMessageQueue<IMessageEnvelope> GetMesssageQueue()
        {
            return null;
        }
        protected override IMessageQueue CreateMessageQueue()
        {
            mq = GetMesssageQueue();// new ProtoMessageQueue(MaxIndexedMemory, true);
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

        public void SendAsync<Z>(Z message) where Z : IMessageEnvelope
        {
            if (IsSessionClosing())
                return;
            try
            {
                SendAsyncInternal(message);
            }
            catch { if (!IsSessionClosing()) throw; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SendAsyncInternal<Z>(Z message) where Z : IMessageEnvelope
        {
            enqueueLock.Take();
            if (IsSessionClosing())
                ReleaseSendResourcesIdempotent();
            if (SendSemaphore.IsTaken() && mq.TryEnqueueMessage(message))
            {
                enqueueLock.Release();
                return;
            }
            enqueueLock.Release();

            if (DropOnCongestion && SendSemaphore.IsTaken())
                return;

            SendSemaphore.Take();
            if (IsSessionClosing())
            {
                ReleaseSendResourcesIdempotent();
                SendSemaphore.Release();
                return;
            }

            // you have to push it to queue because queue also does the processing.
            mq.TryEnqueueMessage(message);
            mq.TryFlushQueue(ref sendBuffer, 0, out int amountWritten);
            FlushSendBuffer(0, amountWritten);

        }

        public void SendAsync<Z, T>(Z envelope, T message) where Z : IMessageEnvelope
        {
            if (IsSessionClosing())
                return;
            try
            {
                SendAsyncInternal(envelope, message);
            }
            catch { if (!IsSessionClosing()) throw; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SendAsyncInternal<Z, T>(Z envelope, T message) where Z : IMessageEnvelope
        {
            enqueueLock.Take();
            if (IsSessionClosing())
                ReleaseSendResourcesIdempotent();
            if (SendSemaphore.IsTaken() && mq.TryEnqueueMessage(envelope, message))
            {
                enqueueLock.Release();
                return;
            }
            enqueueLock.Release();

            if (DropOnCongestion && SendSemaphore.IsTaken())
                return;

            SendSemaphore.Take();
            if (IsSessionClosing())
            {
                ReleaseSendResourcesIdempotent();
                SendSemaphore.Release();
                return;
            }

            // you have to push it to queue because queue also does the processing.
            mq.TryEnqueueMessage(envelope, message);
            mq.TryFlushQueue(ref sendBuffer, 0, out int amountWritten);
            FlushSendBuffer(0, amountWritten);
        }

        protected override void ReleaseReceiveResources()
        {
            base.ReleaseReceiveResources();
            reader.ReleaseResources();
            reader = null;
            mq = null;
        }
    }
}
