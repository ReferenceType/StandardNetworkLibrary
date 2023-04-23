using MessageProtocol;
using Protobuff.Components.Serialiser;
using System;
using System.Net.Sockets;

namespace Protobuff.Components.ProtoTcp
{
    internal class ProtoClientInternal :
        MessageClient<ProtoMessageQueue, MessageEnvelope, GenericMessageSerializer<MessageEnvelope,ProtoSerializer>>
    {
        protected override MessageSession<MessageEnvelope, ProtoMessageQueue> MakeSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            return new ProtoSessionInternal(e, sessionId);
        }
        protected override GenericMessageSerializer<MessageEnvelope, ProtoSerializer> CreateMessageSerializer()
        {
            return new GenericMessageSerializer<MessageEnvelope,ProtoSerializer>();
        }

        //public Action<MessageEnvelope> OnMessageReceived;
        //public bool DeserializeMessages = true;
        //private ConcurrentProtoSerialiser serializer = new ConcurrentProtoSerialiser();
        //private ProtoSessionInternal protoSession;

        //protected virtual void MapReceivedBytes()
        //{
        //    OnBytesReceived += HandleBytes;
        //}

        //private void HandleBytes(byte[] bytes, int offset, int count)
        //{
        //    var msg = serializer.DeserialiseEnvelopedMessage(bytes, offset, count);
        //    OnMessageReceived?.Invoke(msg);
        //}

        //protected override IAsyncSession CreateSession(SocketAsyncEventArgs e, Guid sessionId)
        //{
        //    var ses = new ProtoSessionInternal(e, sessionId);
        //    ses.SocketRecieveBufferSize = SocketRecieveBufferSize;
        //    ses.MaxIndexedMemory = MaxIndexedMemory;
        //    ses.DropOnCongestion = DropOnCongestion;
        //    ses.OnSessionClosed += (id) => OnDisconnected?.Invoke();
        //    protoSession= ses;

        //    if (DeserializeMessages)
        //        MapReceivedBytes();
        //    return ses;
        //}

        //public void SendAsyncMessage(MessageEnvelope message)
        //{
        //    if (protoSession != null)
        //        protoSession.SendAsync(message);
        //}

        //public void SendAsyncMessage<T>(MessageEnvelope envelope, T message) where T : IProtoMessage
        //{
        //    if (protoSession != null)
        //        protoSession.SendAsync(envelope, message);
        //}

    }
}
