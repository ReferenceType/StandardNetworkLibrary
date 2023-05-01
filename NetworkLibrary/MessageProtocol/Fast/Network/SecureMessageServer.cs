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
    public class SecureMessageServer<S> : SslServer
     where S : ISerializer, new()
    {
        public Action<Guid, MessageEnvelope> OnMessageReceived;
        public bool DeserializeMessages = true;
        public GenericMessageAwaiter<MessageEnvelope> awaiter = new GenericMessageAwaiter<MessageEnvelope>();

        private GenericMessageSerializer<S> serializer;
        public SecureMessageServer(int port, X509Certificate2 certificate) : base(port, certificate)
        {
            RemoteCertificateValidationCallback += ValidateCert;
            GatherConfig = ScatterGatherConfig.UseBuffer;
            if (MessageEnvelope.Serializer == null)
            {
                MessageEnvelope.Serializer = new GenericMessageSerializer<S>();
            }
        }

        protected virtual bool ValidateCert(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        public override void StartServer()
        {
            if (DeserializeMessages)
                MapReceivedBytes();

            base.StartServer();
        }

        protected virtual GenericMessageSerializer<S> CreateMessageSerializer()
        {
            return new GenericMessageSerializer<S>();
        }

        protected virtual void MapReceivedBytes()
        {
            serializer = CreateMessageSerializer();
            OnBytesReceived += HandleBytes;
        }

        private void HandleBytes(in Guid guid, byte[] bytes, int offset, int count)
        {
            MessageEnvelope message = serializer.DeserialiseEnvelopedMessage(bytes, offset, count);
            if (!CheckAwaiter(message))
            {
                OnMessageReceived?.Invoke(guid, message);
            }
        }

        protected virtual SecureMessageSession<S> GetSession(Guid guid, SslStream sslStream)
        {
            return new SecureMessageSession<S>(guid, sslStream);
        }
        protected override IAsyncSession CreateSession(Guid guid, ValueTuple<SslStream, IPEndPoint> tuple)
        {
            var session = GetSession(guid, tuple.Item1);//new SecureProtoSessionInternal(guid, tuple.Item1);
            session.MaxIndexedMemory = MaxIndexedMemoryPerClient;
            session.RemoteEndpoint = tuple.Item2;
            return session;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(in Guid clientId, MessageEnvelope message)
        {
            if (Sessions.TryGetValue(clientId, out IAsyncSession session))
                ((SecureMessageSession<S>)session).SendAsync(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage<T>(in Guid clientId, MessageEnvelope envelope, T message)
        {
            if (Sessions.TryGetValue(clientId, out IAsyncSession session))
                ((SecureMessageSession<S>)session).SendAsync(envelope, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(in Guid clientId, MessageEnvelope message, byte[] buffer, int offset, int count)
        {
            message.SetPayload(buffer, offset, count);
            SendAsyncMessage(clientId, message);
        }

        public Task<MessageEnvelope> SendMessageAndWaitResponse<T>(Guid clientId, MessageEnvelope message, byte[] buffer, int offset, int count, int timeoutMs = 10000)
        {
            if (message.MessageId == Guid.Empty)
                message.MessageId = Guid.NewGuid();

            var result = awaiter.RegisterWait(message.MessageId, timeoutMs);
            SendAsyncMessage(clientId, message, buffer, offset, count);
            return result;
        }

        public Task<MessageEnvelope> SendMessageAndWaitResponse<T>(Guid clientId, MessageEnvelope message, T payload, int timeoutMs = 10000)
        {
            if (message.MessageId == Guid.Empty)
                message.MessageId = Guid.NewGuid();

            var task = awaiter.RegisterWait(message.MessageId, timeoutMs);
            SendAsyncMessage(clientId, message, payload);
            return task;
        }

        public Task<MessageEnvelope> SendMessageAndWaitResponse(Guid clientId, MessageEnvelope message, int timeoutMs = 10000)
        {
            if (message.MessageId == Guid.Empty)
                message.MessageId = Guid.NewGuid();

            var task = awaiter.RegisterWait(message.MessageId, timeoutMs);
            SendAsyncMessage(clientId, message);
            return task;
        }
        public IPEndPoint GetIPEndPoint(Guid cliendId)
        {
            return GetSessionEndpoint(cliendId);
        }

        //protected override void HandleBytesReceived(Guid guid, byte[] bytes, int offset, int count)
        //{
        //    if (!DeserializeMessages)
        //    {
        //       base.HandleBytesReceived(guid, bytes, offset, count);
        //        return;
        //    }

        //    E message = serializer.DeserialiseEnvelopedMessage<E>(bytes, offset, count);
        //    if (!CheckAwaiter(message))
        //    {
        //        OnMessageReceived?.Invoke(guid, message);
        //    }
        //}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool CheckAwaiter(MessageEnvelope message)
        {
            if (awaiter.IsWaiting(message.MessageId))
            {
                message.LockBytes();
                awaiter.ResponseArrived(message);
                return true;
            }
            return false;
        }
    }

}
