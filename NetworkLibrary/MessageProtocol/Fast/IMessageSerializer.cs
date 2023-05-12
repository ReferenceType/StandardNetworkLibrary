using MessageProtocol;
using NetworkLibrary.Components;
using NetworkLibrary.MessageProtocol.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkLibrary.MessageProtocol
{
    public interface IMessageSerialiser
    {
        MessageEnvelope DeserialiseEnvelopedMessage(byte[] buffer, int offset, int count);
        RouterHeader DeserialiseOnlyRouterHeader(byte[] buffer, int offset, int count);
        T Deserialize<T>(byte[] data, int offset, int count);
        byte[] EnvelopeMessageWithBytes(MessageEnvelope empyEnvelope, byte[] payloadBuffer, int offset, int count);
        void EnvelopeMessageWithBytes(PooledMemoryStream serialisationStream, MessageEnvelope empyEnvelope, byte[] payloadBuffer, int offset, int count);
        void EnvelopeMessageWithInnerMessage<T>(PooledMemoryStream serialisationStream, MessageEnvelope empyEnvelope, T payload);
        byte[] Serialize<T>(T record);
        byte[] SerializeMessageEnvelope(MessageEnvelope message);
        byte[] SerializeMessageEnvelope<T>(MessageEnvelope empyEnvelope, T payload);
        T UnpackEnvelopedMessage<T>(MessageEnvelope fullEnvelope);

    }
}
