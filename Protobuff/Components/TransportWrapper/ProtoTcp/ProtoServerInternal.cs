using NetworkLibrary.TCP.Base;
using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Net;
using System.Text;
using static Protobuff.ProtoServer;
using System.Net.Sockets;

namespace Protobuff.Components.ProtoTcp
{
    internal class ProtoServerInternal:AsyncTcpServer
    {
        public MessageReceived OnMessageReceived;
        public bool DeserializeMessages = true;
        private readonly ConcurrentProtoSerialiser serializer = new ConcurrentProtoSerialiser();

        internal ProtoServerInternal(int port): base(port)
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
            OnBytesReceived += HandleBytes;
        }

        private void HandleBytes(in Guid guid, byte[] bytes, int offset, int count)
        {
            var msg = serializer.DeserialiseEnvelopedMessage(bytes, offset, count);
            OnMessageReceived?.Invoke(guid, msg);
        }

        protected override IAsyncSession CreateSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            var session = new ProtoSessionInternal(e, sessionId);
            session.socketRecieveBufferSize = ClientReceiveBufsize;
            session.MaxIndexedMemory = MaxIndexedMemoryPerClient;
            session.DropOnCongestion = DropOnBackPressure;
            session.OnSessionClosed += (id) => OnClientDisconnected?.Invoke(id);
            return session;
        }


        public void SendAsyncMessage(in Guid clientId, MessageEnvelope message)
        {
            if (Sessions.TryGetValue(clientId, out var session))
                ((ProtoSessionInternal)session).SendAsync(message);

        }

        public void SendAsyncMessage<T>(in Guid clientId, MessageEnvelope envelope, T message) where T : IProtoMessage
        {
            if (Sessions.TryGetValue(clientId, out var session))
                ((ProtoSessionInternal)session).SendAsync(envelope, message);

        }
    }
}
