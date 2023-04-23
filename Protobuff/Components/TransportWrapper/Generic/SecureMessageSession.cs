﻿//using NetworkLibrary.Components;
//using NetworkLibrary.TCP.SSL.Base;
//using Protobuff.Components.TransportWrapper.Generic.Interfaces;
//using System;
//using System.Net.Security;
//using System.Runtime.CompilerServices;

//namespace Protobuff.Components.TransportWrapper.Generic
//{
//    public abstract class SecureMessageSession<E, Q> : SslSession
//         where E : IMessageEnvelope, new()
//         where Q : class, ISerialisableMessageQueue<E>
//    {
//        protected Q mq;
//        private ByteMessageReader reader;
//        internal SecureMessageSession(Guid sessionId, SslStream sessionStream) : base(sessionId, sessionStream)
//        {
//            reader = new ByteMessageReader(sessionId);
//            reader.OnMessageReady += HandleMessage;
//            UseQueue = false;
//        }

//        protected abstract Q GetMesssageQueue();

//        protected override IMessageQueue CreateMessageQueue()
//        {
//            mq = GetMesssageQueue();
//            return mq;
//        }

//        private void HandleMessage(byte[] arg1, int arg2, int arg3)
//        {
//            base.HandleReceived(arg1, arg2, arg3);
//        }

//        protected override void HandleReceived(byte[] buffer, int offset, int count)
//        {
//            reader.ParseBytes(buffer, offset, count);
//        }

//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public void SendAsync(E message)
//        {
//            if (IsSessionClosing())
//                return;
//            try
//            {
//                SendAsyncInternal(message);
//            }
//            catch { if (!IsSessionClosing()) throw; }
//        }

//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        private void SendAsyncInternal(E message)
//        {
//            enqueueLock.Take();
//            if (IsSessionClosing())
//                ReleaseSendResourcesIdempotent();
//            if (SendSemaphore.IsTaken() && mq.TryEnqueueMessage(message))
//            {
//                enqueueLock.Release();
//                return;
//            }
//            enqueueLock.Release();

//            if (DropOnCongestion && SendSemaphore.IsTaken())
//                return;

//            SendSemaphore.Take();
//            if (IsSessionClosing())
//            {
//                ReleaseSendResourcesIdempotent();
//                SendSemaphore.Release();
//                return;
//            }

//            // you have to push it to queue because queue also does the processing.
//            mq.TryEnqueueMessage(message);
//            mq.TryFlushQueue(ref sendBuffer, 0, out int amountWritten);
//            WriteOnSessionStream(amountWritten);

//        }

//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public void SendAsync<T>(E envelope, T message)
//        {
//            if (IsSessionClosing())
//                return;
//            try
//            {
//                SendAsyncInternal(envelope, message);
//            }
//            catch { if (!IsSessionClosing()) throw; }
//        }

//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        private void SendAsyncInternal<T>(E envelope, T message)
//        {
//            enqueueLock.Take();
//            if (IsSessionClosing())
//                ReleaseSendResourcesIdempotent();
//            if (SendSemaphore.IsTaken() && mq.TryEnqueueMessage(envelope, message))
//            {
//                enqueueLock.Release();
//                return;
//            }
//            enqueueLock.Release();

//            if (DropOnCongestion && SendSemaphore.IsTaken())
//                return;

//            SendSemaphore.Take();
//            if (IsSessionClosing())
//            {
//                ReleaseSendResourcesIdempotent();
//                SendSemaphore.Release();
//                return;
//            }

//            // you have to push it to queue because queue also does the processing.
//            mq.TryEnqueueMessage(envelope, message);
//            mq.TryFlushQueue(ref sendBuffer, 0, out int amountWritten);
//            WriteOnSessionStream(amountWritten);

//        }

//        protected override void ReleaseReceiveResources()
//        {
//            base.ReleaseReceiveResources();
//            reader.ReleaseResources();
//            reader = null;
//            mq = null;
//        }
//    }
//}