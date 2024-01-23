using NetworkLibrary.TCP.Base;
using System;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace NetworkLibrary.MessageProtocol
{
    public class MessageClient<S> : AsyncTpcClient
        where S : ISerializer, new()
    {
        public Action<MessageEnvelope> OnMessageReceived;
        internal GenericMessageAwaiter<MessageEnvelope> Awaiter = new GenericMessageAwaiter<MessageEnvelope>();
        private GenericMessageSerializer<S> serializer;
        private new MessageSession<S> session;
        public MessageClient()
        {
            if (MessageEnvelope.Serializer == null)
            {
                MessageEnvelope.Serializer = new GenericMessageSerializer<S>();
            }
            MapReceivedBytes();
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
                message.LockBytes();
                Awaiter.ResponseArrived(message);
            }
            else
                OnMessageReceived?.Invoke(message);
        }

        private protected virtual MessageSession<S> MakeSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            return new MessageSession<S>(e, sessionId);
        }
        private protected override IAsyncSession CreateSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            var ses = MakeSession(e, sessionId);
            ses.SocketRecieveBufferSize = SocketRecieveBufferSize;
            ses.MaxIndexedMemory = MaxIndexedMemory;
            ses.DropOnCongestion = DropOnCongestion;
            ses.OnSessionClosed += (id) => OnDisconnected?.Invoke();
            ses.UseQueue = false;
            session = ses;

            
            return ses;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(MessageEnvelope message)
        {
            session?.SendAsync(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage<U>(MessageEnvelope envelope, U message)
        {
            session?.SendAsync(envelope, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(MessageEnvelope message, byte[] buffer, int offset, int count)
        {
            message.SetPayload(buffer, offset, count);
            SendAsyncMessage(message);
        }

        public Task<MessageEnvelope> SendMessageAndWaitResponse(MessageEnvelope message, int timeoutMs = 10000)
        {
            message.MessageId = Guid.NewGuid();
            var task = Awaiter.RegisterWait(message.MessageId, timeoutMs);

            SendAsyncMessage(message);
            return task;
        }

        public Task<MessageEnvelope> SendMessageAndWaitResponse<T>(MessageEnvelope message, T payload, int timeoutMs = 10000)
        {
            message.MessageId = Guid.NewGuid();
            var task = Awaiter.RegisterWait(message.MessageId, timeoutMs);

            SendAsyncMessage(message, payload);
            return task;
        }

        public Task<MessageEnvelope> SendMessageAndWaitResponse(MessageEnvelope message, byte[] buffer, int offset, int count, int timeoutMs = 10000)
        {
            message.MessageId = Guid.NewGuid();
            var task = Awaiter.RegisterWait(message.MessageId, timeoutMs);

            SendAsyncMessage(message, buffer, offset, count);
            return task;
        }



        public new Task<bool> ConnectAsync(string host, int port)
        {
            return ConnectAsyncAwaitable(host, port);
        }
    }
}
