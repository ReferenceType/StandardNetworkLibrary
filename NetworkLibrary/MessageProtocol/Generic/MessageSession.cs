using NetworkLibrary.Components;
using NetworkLibrary.MessageProtocol;
using NetworkLibrary.TCP.Base;
using System;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace MessageProtocol
{
    public class MessageSession<E, S> : TcpSession
        where S : ISerializer, new()
        where E : IMessageEnvelope, new()
    {
        protected GenericMessageQueue<S, E> mq;
        private ByteMessageReader reader;
        public MessageSession(SocketAsyncEventArgs acceptedArg, Guid sessionId) : base(acceptedArg, sessionId)
        {
            reader = new ByteMessageReader(sessionId);
            reader.OnMessageReady += HandleMessage;
            UseQueue = false;
        }

        protected virtual GenericMessageQueue<S, E> GetMesssageQueue()
        {
            return new GenericMessageQueue<S, E>(MaxIndexedMemory, true);
        }

        protected override IMessageQueue CreateMessageQueue()
        {
            mq = GetMesssageQueue();
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsync(E message)
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
        private void SendAsyncInternal(E message)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsync<T>(E envelope, T message)
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
        private void SendAsyncInternal<T>(E envelope, T message)
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
