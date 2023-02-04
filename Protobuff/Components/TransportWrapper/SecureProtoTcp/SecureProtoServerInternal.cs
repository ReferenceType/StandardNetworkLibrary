using NetworkLibrary.TCP.SSL.Base;
using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using NetworkLibrary.TCP.Base;
using static Protobuff.ProtoServer;

namespace Protobuff.Components.TransportWrapper.SecureProtoTcp
{
    internal class SecureProtoServerInternal : SslServer
    {
        public MessageReceived OnMessageReceived;
        public bool DeserializeMessages = true;
        ConcurrentProtoSerialiser serializer = new ConcurrentProtoSerialiser();
        public SecureProtoServerInternal(int port, X509Certificate2 certificate) : base(port, certificate)
        {
            RemoteCertificateValidationCallback += ValidateCert;
            GatherConfig = ScatterGatherConfig.UseBuffer;

        }
        private bool ValidateCert(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
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

        protected override IAsyncSession CreateSession(Guid guid, ValueTuple<SslStream, IPEndPoint> tuple)
        {
            var session = new SecureProtoSessionInternal(guid, tuple.Item1);
            session.MaxIndexedMemory = MaxIndexedMemoryPerClient;
            session.RemoteEndpoint = tuple.Item2;
            return session;
        }

        public void SendAsyncMessage(in Guid clientId, MessageEnvelope message)
        {
            if (Sessions.TryGetValue(clientId, out var session))
                ((SecureProtoSessionInternal)session).SendAsync(message);

        }

        public void SendAsyncMessage<T>(in Guid clientId, MessageEnvelope envelope, T message) where T : IProtoMessage
        {
            if (Sessions.TryGetValue(clientId, out var session))
                ((SecureProtoSessionInternal)session).SendAsync(envelope, message);

        }
    }
}
