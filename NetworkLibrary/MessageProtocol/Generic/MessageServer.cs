using NetworkLibrary.MessageProtocol;
using NetworkLibrary.TCP.Base;
using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace MessageProtocol
{
    public class MessageServer<E, S> : AsyncTcpServer
        where E : IMessageEnvelope, new()
        where S : ISerializer, new()
    {
        public Action<Guid, E> OnMessageReceived;
        public bool DeserializeMessages = true;

        private GenericMessageSerializer<E,S> serializer;
        public GenericMessageAwaiter<E> Awaiter = new GenericMessageAwaiter<E>();
        public MessageServer(int port) : base(port)
        {

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

        private void HandleBytes(in Guid guid, byte[] bytes, int offset, int count)
        {
            E message = serializer.DeserialiseEnvelopedMessage(bytes, offset, count);
            if (!CheckAwaiter(message))
            {
                OnMessageReceived?.Invoke(guid, message);
            }
        }

        protected virtual MessageSession<E, S> MakeSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            return new MessageSession<E, S>(e, sessionId);
        }
        protected override IAsyncSession CreateSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            var session = MakeSession(e, sessionId);//new GenericMessageSession(e, sessionId);
            session.SocketRecieveBufferSize = ClientReceiveBufsize;
            session.MaxIndexedMemory = MaxIndexedMemoryPerClient;
            session.DropOnCongestion = DropOnBackPressure;
            session.OnSessionClosed += (id) => OnClientDisconnected?.Invoke(id);
            return session;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(in Guid clientId, E message)
        {
            if (Sessions.TryGetValue(clientId, out var session))
                ((MessageSession<E, S>)session).SendAsync(message);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage<T>(in Guid clientId, E envelope, T message)
        {
            if (Sessions.TryGetValue(clientId, out var session))
                ((MessageSession<E, S>)session).SendAsync(envelope, message);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(in Guid clientId, E message, byte[] buffer, int offset, int count)
        {
            message.SetPayload(buffer, offset, count);
            SendAsyncMessage(clientId, message);
        }

        public Task<E> SendMessageAndWaitResponse<T>(Guid clientId, E message, byte[] buffer, int offset, int count, int timeoutMs = 10000)
        {
            if (message.MessageId == Guid.Empty)
                message.MessageId = Guid.NewGuid();

            var result = Awaiter.RegisterWait(message.MessageId, timeoutMs);
            SendAsyncMessage(clientId, message, buffer, offset, count);
            return result;
        }

        public Task<E> SendMessageAndWaitResponse<T>(Guid clientId, E message, T payload, int timeoutMs = 10000)
        {
            if (message.MessageId == Guid.Empty)
                message.MessageId = Guid.NewGuid();

            var task = Awaiter.RegisterWait(message.MessageId, timeoutMs);
            SendAsyncMessage(clientId, message, payload);
            return task;
        }

        public Task<E> SendMessageAndWaitResponse(Guid clientId, E message, int timeoutMs = 10000)
        {
            if (message.MessageId == Guid.Empty)
                message.MessageId = Guid.NewGuid();

            var task = Awaiter.RegisterWait(message.MessageId, timeoutMs);
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
        //        base.HandleBytesReceived(guid, bytes, offset, count);
        //        return;
        //    }

        //    E message = serializer.DeserialiseEnvelopedMessage<E>(bytes, offset, count);
        //    if (!CheckAwaiter(message))
        //    {
        //        OnMessageReceived?.Invoke(guid, message);
        //    }
        //}

        protected bool CheckAwaiter(E message)
        {
            if (Awaiter.IsWaiting(message.MessageId))
            {
                message.LockBytes();
                Awaiter.ResponseArrived(message);
                return true;
            }
            return false;
        }
    }
}
