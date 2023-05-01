using NetworkLibrary.TCP.Base;
using NetworkLibrary.TCP.SSL.Base;
using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Serialization;
using MessageProtocol.Serialization;


namespace NetworkLibrary.MessageProtocol
{
    public class SecureMessageClient<S> : SslClient
    where S : ISerializer, new()
    {
        public Action<MessageEnvelope> OnMessageReceived;
        public bool DeserializeMessages = true;
        private GenericMessageSerializer<S> serializer;
        private SecureMessageSession<S> messageSession;
        public GenericMessageAwaiter<MessageEnvelope> Awaiter = new GenericMessageAwaiter<MessageEnvelope>();


        public SecureMessageClient(X509Certificate2 certificate) : base(certificate)
        {
            RemoteCertificateValidationCallback += ValidateCert;
            GatherConfig = ScatterGatherConfig.UseBuffer;
            if(MessageEnvelope.Serializer == null)
            {
                MessageEnvelope.Serializer = new GenericMessageSerializer<S>();
            }
        }

        protected virtual bool ValidateCert(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        protected virtual void MapReceivedBytes()
        {
            serializer = new GenericMessageSerializer<S>();
            OnBytesReceived += HandleBytes;
        }
        private void HandleBytes(byte[] bytes, int offset, int count)
        {
            MessageEnvelope message = serializer.DeserialiseEnvelopedMessage(bytes, offset, count);

            if (Awaiter.IsWaiting(message.MessageId))
            {
                message.LockBytes();
                Awaiter.ResponseArrived(message);
            }
            else
                OnMessageReceived?.Invoke(message);
        }

        protected virtual SecureMessageSession <S> GetSession(Guid guid, SslStream sslStream)
        {
            return new SecureMessageSession<S>(guid, sslStream);
        }
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
        public void SendAsyncMessage(MessageEnvelope message)
        {
            if (clientSession != null)
                messageSession.SendAsync(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage<U>(MessageEnvelope envelope, U message)
        {
            if (clientSession != null)
                messageSession.SendAsync(envelope, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(MessageEnvelope message, byte[] buffer, int offset, int count)
        {
            message.SetPayload(buffer, offset, count);
            SendAsyncMessage(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<MessageEnvelope> SendMessageAndWaitResponse(MessageEnvelope message, int timeoutMs = 10000)
        {
            message.MessageId = Guid.NewGuid();
            var task = Awaiter.RegisterWait(message.MessageId, timeoutMs);

            SendAsyncMessage(message);
            return task;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<MessageEnvelope> SendMessageAndWaitResponse<T>(MessageEnvelope message, T payload, int timeoutMs = 10000)
        {
            message.MessageId = Guid.NewGuid();
            var task = Awaiter.RegisterWait(message.MessageId, timeoutMs);

            SendAsyncMessage(message, payload);
            return task;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<MessageEnvelope> SendMessageAndWaitResponse(MessageEnvelope message, byte[] buffer, int offset, int count, int timeoutMs = 10000)
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
