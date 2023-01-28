using NetworkLibrary.Components;
using NetworkLibrary.UDP.Secure;
using NetworkLibrary.Utils;
using ProtoBuf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace Protobuff
{
    internal class EncryptedUdpProtoClient : SecureUdpClient
    {
        public Action<MessageEnvelope> OnMessageReceived;
        private readonly ConcurrentProtoSerialiser serialiser = new ConcurrentProtoSerialiser();
        private readonly SharerdMemoryStreamPool streamPool = new SharerdMemoryStreamPool();

        public EncryptedUdpProtoClient(ConcurrentAesAlgorithm algorithm) : base(algorithm) {}

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
    }
}
