using MessageProtocol;
using NetworkLibrary.Components;
using NetworkLibrary;
using NetworkLibrary.MessageProtocol.Serialization;
using NetworkLibrary.UDP.Secure;
using NetworkLibrary.Utils;
using Protobuff.Components.Serialiser;
using System;
using NetworkLibrary.MessageProtocol;

namespace Protobuff
{
    internal class EncryptedUdpProtoClient : SecureUdpClient
    {
        public Action<MessageEnvelope> OnMessageReceived;
        private readonly GenericMessageSerializer<ProtoSerializer> serialiser = new GenericMessageSerializer< ProtoSerializer>();
        //private readonly ConcurrentProtoSerialiser serialiser =  new ConcurrentProtoSerialiser();
         private readonly SharerdMemoryStreamPool streamPool = new SharerdMemoryStreamPool();

        public EncryptedUdpProtoClient(ConcurrentAesAlgorithm algorithm) : base(algorithm) { }

        protected override void HandleDecrypedBytes(byte[] buffer, int offset, int count)
        {
            var msg = serialiser.DeserialiseEnvelopedMessage(buffer, offset, count);
            OnMessageReceived?.Invoke(msg);
        }

        public void SendAsyncMessage(MessageEnvelope message)
        {
            var serialisationStream = streamPool.RentStream();
            serialiser.EnvelopeMessageWithBytes(serialisationStream,
                message, message.Payload, message.PayloadOffset, message.PayloadCount);

            SendAsync(serialisationStream.GetBuffer(), 0, (int)serialisationStream.Position);
            streamPool.ReturnStream(serialisationStream);
        }

        public void SendAsyncMessage<T>(MessageEnvelope message, T innerMessage) where T : IProtoMessage
        {
            var serialisationStream = streamPool.RentStream();
            serialiser.EnvelopeMessageWithInnerMessage(serialisationStream, message, innerMessage);

            SendAsync(serialisationStream.GetBuffer(), 0, (int)serialisationStream.Position);
            streamPool.ReturnStream(serialisationStream);
        }

        public void SendAsyncMessage(MessageEnvelope message, byte[] payload, int offset, int count)
        {
            var serialisationStream = streamPool.RentStream();
            serialiser.EnvelopeMessageWithBytes(serialisationStream, message, payload, offset, count);

            SendAsync(serialisationStream.GetBuffer(), 0, (int)serialisationStream.Position);
            streamPool.ReturnStream(serialisationStream);
        }

        public void SendAsyncMessage(MessageEnvelope message, Action<PooledMemoryStream> serializationCallback) 
        {
            var serialisationStream = streamPool.RentStream();
            serialiser.EnvelopeMessageWithInnerMessage(serialisationStream, message, serializationCallback);

            SendAsync(serialisationStream.GetBuffer(), 0, (int)serialisationStream.Position);
            streamPool.ReturnStream(serialisationStream);
        }
    }
}
