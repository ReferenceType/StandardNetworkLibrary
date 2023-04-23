using System;
using System.Net.Sockets;

namespace Protobuff.Components.TransportWrapper.Generic
{

    internal class GenericMessageServer : AsyncTcpServer
    {
        public delegate void MessageReceived(in Guid clientId, IMessageEnvelope message);
        public MessageReceived OnMessageReceived;
        public bool DeserializeMessages = true;
        private IMessageSerialiser serializer;

        internal GenericMessageServer(int port) : base(port)
        {

        }

        public override void StartServer()
        {

            if (DeserializeMessages)
                MapReceivedBytes();

            base.StartServer();
        }
        protected virtual void MapReceivedBytes()
        {
            serializer = CreateSerialiser();
            OnBytesReceived += HandleBytes;
        }
        protected virtual IMessageSerialiser CreateSerialiser()
        {
            return null;
        }
        private void HandleBytes(in Guid guid, byte[] bytes, int offset, int count)
        {
            var msg = serializer.DeserialiseEnvelopedMessage(bytes, offset, count);
            OnMessageReceived?.Invoke(guid, msg);
        }

        protected override IAsyncSession CreateSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            var session = new GenericMessageSession(e, sessionId);
            session.SocketRecieveBufferSize = ClientReceiveBufsize;
            session.MaxIndexedMemory = MaxIndexedMemoryPerClient;
            session.DropOnCongestion = DropOnBackPressure;
            session.OnSessionClosed += (id) => OnClientDisconnected?.Invoke(id);
            return session;
        }


        public void SendAsyncMessage(in Guid clientId, IMessageEnvelope message)
        {
            if (Sessions.TryGetValue(clientId, out var session))
                ((GenericMessageSession)session).SendAsync(message);

        }

        public void SendAsyncMessage<T>(in Guid clientId, IMessageEnvelope envelope, T message)
        {
            if (Sessions.TryGetValue(clientId, out var session))
                ((GenericMessageSession)session).SendAsync(envelope, message);

        }
    }
}
