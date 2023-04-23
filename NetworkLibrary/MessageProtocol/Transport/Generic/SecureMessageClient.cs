﻿using NetworkLibrary.TCP.Base;
using NetworkLibrary.TCP.SSL.Base;
using System;
using System.Net;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace MessageProtocol
{
    public abstract class SecureMessageClient<Q, E, S> : SslClient
        where Q : class, ISerialisableMessageQueue<E>
        where E : IMessageEnvelope, new()
        where S : IMessageSerialiser<E>
    {
        public Action<E> OnMessageReceived;
        public bool DeserializeMessages = true;
        private S serializer;
        private SecureMessageSession<E, Q> messageSession;
        public GenericMessageAwaiter<E> Awaiter = new GenericMessageAwaiter<E>();


        public SecureMessageClient(X509Certificate2 certificate) : base(certificate)
        {
            RemoteCertificateValidationCallback += ValidateCert;
            GatherConfig = ScatterGatherConfig.UseBuffer;
        }

        protected virtual bool ValidateCert(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        protected abstract S CreateMessageSerializer();

        protected virtual void MapReceivedBytes()
        {
            serializer = CreateMessageSerializer();
            OnBytesReceived += HandleBytes;
        }
        private void HandleBytes(byte[] bytes, int offset, int count)
        {
            E message = serializer.DeserialiseEnvelopedMessage(bytes, offset, count);

            if (Awaiter.IsWaiting(message.MessageId))
            {
                message.LockBytes();
                Awaiter.ResponseArrived(message);
            }
            else
                OnMessageReceived?.Invoke(message);
        }

        protected abstract SecureMessageSession<E, Q> GetSession(Guid guid, SslStream sslStream);
        protected override IAsyncSession CreateSession(Guid guid, ValueTuple<SslStream, IPEndPoint> tuple)
        {
            var session = GetSession(guid, tuple.Item1);//new SecureProtoSessionInternal(guid, tuple.Item1);
            session.MaxIndexedMemory = MaxIndexedMemory;
            session.RemoteEndpoint = tuple.Item2;
            messageSession = session;
            if (DeserializeMessages)
                MapReceivedBytes();
            return session;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(E message)
        {
            if (clientSession != null)
                messageSession.SendAsync(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage<U>(E envelope, U message)
        {
            if (clientSession != null)
                messageSession.SendAsync(envelope, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(E message, byte[] buffer, int offset, int count)
        {
            message.SetPayload(buffer, offset, count);
            SendAsyncMessage(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<E> SendMessageAndWaitResponse(E message, int timeoutMs = 10000)
        {
            message.MessageId = Guid.NewGuid();
            var task = Awaiter.RegisterWait(message.MessageId, timeoutMs);

            SendAsyncMessage(message);
            return task;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<E> SendMessageAndWaitResponse<T>(E message, T payload, int timeoutMs = 10000)
        {
            message.MessageId = Guid.NewGuid();
            var task = Awaiter.RegisterWait(message.MessageId, timeoutMs);

            SendAsyncMessage(message, payload);
            return task;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<E> SendMessageAndWaitResponse(E message, byte[] buffer, int offset, int count, int timeoutMs = 10000)
        {
            message.MessageId = Guid.NewGuid();
            var task = Awaiter.RegisterWait(message.MessageId, timeoutMs);

            SendAsyncMessage(message, buffer, offset, count);
            return task;
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //protected override void HandleBytesReceived(Guid sesonId, byte[] bytes, int offset, int count)
        //{
        //    if (!DeserializeMessages)
        //    {
        //        base.HandleBytesReceived(sesonId,bytes, offset, count);
        //        return;
        //    }

        //    E message = serializer.DeserialiseEnvelopedMessage<E>(bytes, offset, count);

        //    if (awaiter.IsWaiting(message.MessageId))
        //    {
        //        message.LockBytes();
        //        awaiter.ResponseArrived(message);
        //    }
        //    else
        //        OnMessageReceived?.Invoke(message);

        //}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public new Task<bool> ConnectAsync(string host, int port)
        {
            return ConnectAsyncAwaitable(host, port);
        }
    }
}