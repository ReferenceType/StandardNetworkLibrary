using MessageProtocol;
using Protobuff.Components.Serialiser;
using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Protobuff.Components.TransportWrapper.SecureProtoTcp
{
    internal class SecureProtoServerInternal : SecureMessageServer<ProtoMessageQueue, MessageEnvelope, GenericMessageSerializer<MessageEnvelope, ProtoSerializer>>
    {
        public SecureProtoServerInternal(int port, X509Certificate2 certificate) : base(port, certificate)
        {

        }

        protected override GenericMessageSerializer<MessageEnvelope, ProtoSerializer> CreateMessageSerializer()
        {
            return new GenericMessageSerializer<MessageEnvelope, ProtoSerializer>();
        }

        protected override SecureMessageSession<MessageEnvelope, ProtoMessageQueue> GetSession(Guid guid, SslStream sslStream)
        {
            return new SecureProtoSessionInternal(guid, sslStream);
        }
        //public MessageReceived OnMessageReceived;
        //public bool DeserializeMessages = true;
        //ConcurrentProtoSerialiser serializer = new ConcurrentProtoSerialiser();
        //public SecureProtoServerInternal(int port, X509Certificate2 certificate) : base(port, certificate)
        //{
        //    RemoteCertificateValidationCallback += ValidateCert;
        //    GatherConfig = ScatterGatherConfig.UseBuffer;

        //}
        //private bool ValidateCert(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        //{
        //    return true;
        //}
        //public override void StartServer()
        //{
        //    if (DeserializeMessages)
        //        MapReceivedBytes();

        //    base.StartServer();
        //}
        //protected virtual void MapReceivedBytes()
        //{
        //    OnBytesReceived += HandleBytes;
        //}

        //private void HandleBytes(in Guid guid, byte[] bytes, int offset, int count)
        //{
        //    var msg = serializer.DeserialiseEnvelopedMessage(bytes, offset, count);
        //    OnMessageReceived?.Invoke(guid, msg);
        //}

        //protected override IAsyncSession CreateSession(Guid guid, ValueTuple<SslStream, IPEndPoint> tuple)
        //{
        //    var session = new SecureProtoSessionInternal(guid, tuple.Item1);
        //    session.MaxIndexedMemory = MaxIndexedMemoryPerClient;
        //    session.RemoteEndpoint = tuple.Item2;
        //    return session;
        //}

        //public void SendAsyncMessage(in Guid clientId, MessageEnvelope message)
        //{
        //    if (Sessions.TryGetValue(clientId, out var session))
        //        ((SecureProtoSessionInternal)session).SendAsync(message);

        //}

        //public void SendAsyncMessage<T>(in Guid clientId, MessageEnvelope envelope, T message) where T : IProtoMessage
        //{
        //    if (Sessions.TryGetValue(clientId, out var session))
        //        ((SecureProtoSessionInternal)session).SendAsync(envelope, message);

        //}

    }
}
