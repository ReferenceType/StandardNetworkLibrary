using NetworkLibrary.MessageProtocol;
using NetworkLibrary.TCP.Base;
using NetworkLibrary.TCP.SSL.Base;
using System;
using System.Net;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace MessageProtocol
{
    public class SecureMessageServer<E, S> : SslServer
         where E : IMessageEnvelope, new()
         where S : ISerializer, new()
    {
        public Action<Guid, E> OnMessageReceived;
        public bool DeserializeMessages = true;
        internal GenericMessageAwaiter<E> awaiter = new GenericMessageAwaiter<E>();

        private GenericMessageSerializer<E, S> serializer;
        public SecureMessageServer(int port, X509Certificate2 certificate) : base(port, certificate)
        {
            RemoteCertificateValidationCallback += ValidateCert;
            GatherConfig = ScatterGatherConfig.UseBuffer;
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

        protected virtual GenericMessageSerializer<E, S> CreateMessageSerializer()
        {
            return new GenericMessageSerializer<E, S>();
        }

        protected virtual void MapReceivedBytes()
        {
            serializer = CreateMessageSerializer();
            OnBytesReceived += HandleBytes;
        }

        private void HandleBytes(Guid guid, byte[] bytes, int offset, int count)
        {
            E message = serializer.DeserialiseEnvelopedMessage(bytes, offset, count);
            if (!CheckAwaiter(message))
            {
                OnMessageReceived?.Invoke(guid, message);
            }
        }

        private protected virtual SecureMessageSession<E, S> GetSession(Guid guid, SslStream sslStream)
        {
            return new SecureMessageSession<E, S>(guid, sslStream);
        }
        private protected override IAsyncSession CreateSession(Guid guid, ValueTuple<SslStream, IPEndPoint> tuple)
        {
            var session = GetSession(guid, tuple.Item1);//new SecureProtoSessionInternal(guid, tuple.Item1);
            session.MaxIndexedMemory = MaxIndexedMemoryPerClient;
            session.RemoteEndpoint = tuple.Item2;
            return session;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(Guid clientId, E message)
        {
            if (Sessions.TryGetValue(clientId, out IAsyncSession session))
                ((SecureMessageSession<E, S>)session).SendAsync(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage<T>(Guid clientId, E envelope, T message)
        {
            if (Sessions.TryGetValue(clientId, out IAsyncSession session))
                ((SecureMessageSession<E, S>)session).SendAsync(envelope, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(Guid clientId, E message, byte[] buffer, int offset, int count)
        {
            message.SetPayload(buffer, offset, count);
            SendAsyncMessage(clientId, message);
        }

        public Task<E> SendMessageAndWaitResponse<T>(Guid clientId, E message, byte[] buffer, int offset, int count, int timeoutMs = 10000)
        {
            if (message.MessageId == Guid.Empty)
                message.MessageId = Guid.NewGuid();

            var result = awaiter.RegisterWait(message.MessageId, timeoutMs);
            SendAsyncMessage(clientId, message, buffer, offset, count);
            return result;
        }

        public Task<E> SendMessageAndWaitResponse<T>(Guid clientId, E message, T payload, int timeoutMs = 10000)
        {
            if (message.MessageId == Guid.Empty)
                message.MessageId = Guid.NewGuid();

            var task = awaiter.RegisterWait(message.MessageId, timeoutMs);
            SendAsyncMessage(clientId, message, payload);
            return task;
        }

        public Task<E> SendMessageAndWaitResponse(Guid clientId, E message, int timeoutMs = 10000)
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
        protected bool CheckAwaiter(E message)
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
