using NetworkLibrary.Components;
using NetworkLibrary.TCP.Base;
using NetworkLibrary.TCP.SSL.Base;
using System;
using System.Net;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace NetworkLibrary.MessageProtocol
{
    public class SecureMessageClient<S> : SslClient
    where S : ISerializer, new()
    {
        public Action<MessageEnvelope> OnMessageReceived;
        private GenericMessageSerializer<S> serializer;
        private SecureMessageSession<S> messageSession;
        internal GenericMessageAwaiter<MessageEnvelope> Awaiter = new GenericMessageAwaiter<MessageEnvelope>();


        public SecureMessageClient(X509Certificate2 certificate) : base(certificate)
        {
            RemoteCertificateValidationCallback += ValidateCert;
            GatherConfig = ScatterGatherConfig.UseBuffer;
            if (MessageEnvelope.Serializer == null)
            {
                MessageEnvelope.Serializer = new GenericMessageSerializer<S>();
            }
            MapReceivedBytes();
        }
        public SecureMessageClient() : base()
        {
            RemoteCertificateValidationCallback += ValidateCert;
            GatherConfig = ScatterGatherConfig.UseBuffer;
            if (MessageEnvelope.Serializer == null)
            {
                MessageEnvelope.Serializer = new GenericMessageSerializer<S>();
            }
            MapReceivedBytes();
        }

        protected virtual bool ValidateCert(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        protected virtual void MapReceivedBytes()
        {
            serializer = new GenericMessageSerializer<S>();
            OnBytesReceived = HandleBytes;
        }
        private void HandleBytes(byte[] bytes, int offset, int count)
        {
            MessageEnvelope message = serializer.DeserialiseEnvelopedMessage(bytes, offset, count);

            if (Awaiter.IsWaiting(message.MessageId))
            {
                Awaiter.ResponseArrived(message);
            }
            else
                OnMessageReceived?.Invoke(message);
        }

        private protected virtual SecureMessageSession<S> GetSession(Guid guid, SslStream sslStream)
        {
            return new SecureMessageSession<S>(guid, sslStream);
        }
        private protected override IAsyncSession CreateSession(Guid guid, ValueTuple<SslStream, IPEndPoint> tuple)
        {
            var session = GetSession(guid, tuple.Item1);
            session.MaxIndexedMemory = MaxIndexedMemory;
            session.RemoteEndpoint = tuple.Item2;
            messageSession = session;
            
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
        public void SendAsyncMessage(MessageEnvelope message, Action<PooledMemoryStream> serializationCallback)
        {
            if (clientSession != null)
                messageSession.SendAsync(message, serializationCallback);
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



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public new Task<bool> ConnectAsync(string host, int port)
        {
            return ConnectAsyncAwaitable(host, port);
        }

        public override void Dispose()
        {
            OnMessageReceived = null;
            base.Dispose();
        }
    }

}
