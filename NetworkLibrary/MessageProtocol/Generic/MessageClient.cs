using NetworkLibrary.MessageProtocol;
using NetworkLibrary.TCP.Base;
using System;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace MessageProtocol
{
    public class MessageClient< E, S> : AsyncTpcClient
        where E : IMessageEnvelope, new()
        where S : ISerializer, new()
    {
        public Action<E> OnMessageReceived;
        public bool DeserializeMessages = true;
        private GenericMessageSerializer<E,S> serializer;
        private new MessageSession<E, S> session;
        public GenericMessageAwaiter<E> Awaiter = new GenericMessageAwaiter<E>();
       
        protected virtual void MapReceivedBytes()
        {
            serializer = new GenericMessageSerializer<E, S>();
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

        protected virtual MessageSession<E, S> MakeSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            return new MessageSession<E, S>(e,sessionId);
        }
        protected override IAsyncSession CreateSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            var ses = MakeSession(e, sessionId);
            ses.SocketRecieveBufferSize = SocketRecieveBufferSize;
            ses.MaxIndexedMemory = MaxIndexedMemory;
            ses.DropOnCongestion = DropOnCongestion;
            ses.OnSessionClosed += (id) => OnDisconnected?.Invoke();
            session = ses;

            if (DeserializeMessages)
                MapReceivedBytes();
            return ses;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(E message)
        {
            //if (session != null)
                session?.SendAsync(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage<U>(E envelope, U message)
        {
            //if (session != null)
                session?.SendAsync(envelope, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(E message, byte[] buffer, int offset, int count)
        {
            message.SetPayload(buffer, offset, count);
            SendAsyncMessage(message);
        }

        public Task<E> SendMessageAndWaitResponse(E message, int timeoutMs = 10000)
        {
            message.MessageId = Guid.NewGuid();
            var task = Awaiter.RegisterWait(message.MessageId, timeoutMs);

            SendAsyncMessage(message);
            return task;
        }

        public Task<E> SendMessageAndWaitResponse<T>(E message, T payload, int timeoutMs = 10000)
        {
            message.MessageId = Guid.NewGuid();
            var task = Awaiter.RegisterWait(message.MessageId, timeoutMs);

            SendAsyncMessage(message, payload);
            return task;
        }

        public Task<E> SendMessageAndWaitResponse(E message, byte[] buffer, int offset, int count, int timeoutMs = 10000)
        {
            message.MessageId = Guid.NewGuid();
            var task = Awaiter.RegisterWait(message.MessageId, timeoutMs);

            SendAsyncMessage(message, buffer, offset, count);
            return task;
        }


        //protected override void HandleBytesRecieved(byte[] bytes, int offset, int count)
        //{
        //    if (!DeserializeMessages)
        //    {
        //        base.HandleBytesRecieved(bytes, offset, count);
        //        return;
        //    }

        //    E message = serializer.DeserialiseEnvelopedMessage<E>(bytes, offset, count);

        //    if (Awaiter.IsWaiting(message.MessageId))
        //    {
        //        message.LockBytes();
        //        Awaiter.ResponseArrived(message);
        //    }
        //    else
        //        OnMessageReceived?.Invoke(message);

        //}

        public new Task<bool> ConnectAsync(string host, int port)
        {
            return ConnectAsyncAwaitable(host, port);
        }

    }
}
