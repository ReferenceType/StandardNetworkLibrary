using NetworkLibrary.Components;

namespace NetworkLibrary.MessageProtocol
{
    public interface IMessageSerialiser<E>
        where E : IMessageEnvelope, new()
    {
        E DeserialiseEnvelopedMessage(byte[] buffer, int offset, int count);
        R DeserialiseOnlyRouterHeader<R>(byte[] buffer, int offset, int count) where R : IRouterHeader, new();
        T Deserialize<T>(byte[] data, int offset, int count);
        byte[] EnvelopeMessageWithBytes(E empyEnvelope, byte[] payloadBuffer, int offset, int count);
        void EnvelopeMessageWithBytes(PooledMemoryStream serialisationStream, E empyEnvelope, byte[] payloadBuffer, int offset, int count);
        void EnvelopeMessageWithInnerMessage<T>(PooledMemoryStream serialisationStream, E empyEnvelope, T payload);
        byte[] Serialize<T>(T record);
        byte[] SerializeMessageEnvelope(E message);
        byte[] SerializeMessageEnvelope<T>(E empyEnvelope, T payload);
        T UnpackEnvelopedMessage<T>(E fullEnvelope);

    }
}

