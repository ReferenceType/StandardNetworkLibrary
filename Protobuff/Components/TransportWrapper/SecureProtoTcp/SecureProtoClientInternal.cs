using MessageProtocol;
using Protobuff.Components.Serialiser;
using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Protobuff.Components.TransportWrapper.SecureProtoTcp
{
    public class SecureProtoClientInternal : SecureMessageClient< MessageEnvelope, ProtoSerializer>
    {

        public SecureProtoClientInternal(X509Certificate2 certificate) : base(certificate)
        {

        }

       

        //public Action<MessageEnvelope> OnMessageReceived;
        //public bool DeserializeMessages = true;
        //private ConcurrentProtoSerialiser serializer = new ConcurrentProtoSerialiser();
        //private SecureProtoSessionInternal protoSession;
        //public SecureProtoClientInternal(X509Certificate2 certificate) : base(certificate)
        //{
        //    RemoteCertificateValidationCallback += ValidateCert;
        //    GatherConfig = ScatterGatherConfig.UseBuffer;
        //}

        //private bool ValidateCert(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        //{
        //    return true;
        //}

        //protected virtual void MapReceivedBytes()
        //{
        //    OnBytesReceived += HandleBytes;
        //}
        //private void HandleBytes(byte[] bytes, int offset, int count)
        //{
        //    var msg = serializer.DeserialiseEnvelopedMessage(bytes, offset, count);
        //    OnMessageReceived?.Invoke(msg);
        //}
        //protected override IAsyncSession CreateSession(Guid guid, ValueTuple<SslStream, IPEndPoint> tuple)
        //{
        //    var session = new SecureProtoSessionInternal(guid, tuple.Item1);
        //    session.MaxIndexedMemory = MaxIndexedMemory;
        //    session.RemoteEndpoint = tuple.Item2;
        //    protoSession = session;
        //    if (DeserializeMessages)
        //        MapReceivedBytes();
        //    return session;
        //}
        //public void SendAsyncMessage(MessageEnvelope message)
        //{
        //    if (protoSession != null)
        //        protoSession.SendAsync(message);

        //}
        //public void SendAsyncMessage<T>(MessageEnvelope envelope, T message) where T : IProtoMessage
        //{
        //    if (clientSession != null)
        //        protoSession.SendAsync(envelope, message);

        //}


    }
}
