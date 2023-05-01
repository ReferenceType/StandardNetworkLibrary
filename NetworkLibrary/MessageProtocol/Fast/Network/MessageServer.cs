using NetworkLibrary.TCP.Base;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Serialization;
using MessageProtocol.Serialization;
using MessageProtocol;

namespace NetworkLibrary.MessageProtocol
{
    public class MessageServer<S> : AsyncTcpServer
        where S : ISerializer, new()
    {
        public Action<Guid, MessageEnvelope> OnMessageReceived;
        public bool DeserializeMessages = true;

        private GenericMessageSerializer<S> serializer;
        public GenericMessageAwaiter<MessageEnvelope> Awaiter = new GenericMessageAwaiter<MessageEnvelope>();
        public MessageServer(int port) : base(port)
        {
            if (MessageEnvelope.Serializer == null)
            {
                MessageEnvelope.Serializer = new GenericMessageSerializer<S>();
            }
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

        protected virtual MessageSession<S> MakeSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            return new MessageSession<S>(e, sessionId);
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
        public void SendAsyncMessage(in Guid clientId, MessageEnvelope message)
        {
            if (Sessions.TryGetValue(clientId, out var session))
                ((MessageSession<S>)session).SendAsync(message);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage<T>(in Guid clientId, MessageEnvelope envelope, T message)
        {
            if (Sessions.TryGetValue(clientId, out var session))
                ((MessageSession<S>)session).SendAsync(envelope, message);

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

            var result = Awaiter.RegisterWait(message.MessageId, timeoutMs);
            SendAsyncMessage(clientId, message, buffer, offset, count);
            return result;
        }

        public Task<MessageEnvelope> SendMessageAndWaitResponse<T>(Guid clientId, MessageEnvelope message, T payload, int timeoutMs = 10000)
        {
            if (message.MessageId == Guid.Empty)
                message.MessageId = Guid.NewGuid();

            var task = Awaiter.RegisterWait(message.MessageId, timeoutMs);
            SendAsyncMessage(clientId, message, payload);
            return task;
        }

        public Task<MessageEnvelope> SendMessageAndWaitResponse(Guid clientId, MessageEnvelope message, int timeoutMs = 10000)
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

        protected bool CheckAwaiter(MessageEnvelope message)
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
