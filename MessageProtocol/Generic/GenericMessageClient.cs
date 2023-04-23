using NetworkLibrary.TCP.Base;
using System;
using System.Net.Sockets;

namespace Protobuff.Components.TransportWrapper.Generic
{
    internal class GenericMessageClient : AsyncTpcClient
    {
        public Action<IMessageEnvelope> OnMessageReceived;
        public bool DeserializeMessages = true;
        private IMessageSerialiser serializer;
        private GenericMessageSession session;

        protected virtual void MapReceivedBytes()
        {
            OnBytesReceived += HandleBytes;
        }

        private void HandleBytes(byte[] bytes, int offset, int count)
        {
            var msg = serializer.DeserialiseEnvelopedMessage(bytes, offset, count);
            OnMessageReceived?.Invoke(msg);
        }

        protected override IAsyncSession CreateSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            var ses = new GenericMessageSession(e, sessionId);
            ses.SocketRecieveBufferSize = SocketRecieveBufferSize;
            ses.MaxIndexedMemory = MaxIndexedMemory;
            ses.DropOnCongestion = DropOnCongestion;
            ses.OnSessionClosed += (id) => OnDisconnected?.Invoke();
            session = ses;

            if (DeserializeMessages)
                MapReceivedBytes();
            return ses;
        }

        public void SendAsyncMessage(IMessageEnvelope message)
        {
            if (session != null)
                session.SendAsync(message);
        }

        public void SendAsyncMessage<T>(IMessageEnvelope envelope, T message)
        {
            if (session != null)
                session.SendAsync(envelope, message);
        }
    }
}
