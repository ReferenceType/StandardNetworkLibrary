//using System.IO;

//namespace MessageProtocol
//{
//    public interface IMessageSerialiser<K>
//    {
//        E DeserialiseEnvelopedMessage<E>(byte[] buffer, int offset, int count) where E : IMessageEnvelope;
//        R DeserialiseOnlyRouterHeader<R>(byte[] buffer, int offset, int count) where R : IRouterHeader;
//        T Deserialize<T>(byte[] data, int offset, int count);
//        byte[] EnvelopeMessageWithBytes<E>(E empyEnvelope, byte[] payloadBuffer, int offset, int count) where E : IMessageEnvelope;
//        void EnvelopeMessageWithBytes<E>(Stream serialisationStream, E empyEnvelope, byte[] payloadBuffer, int offset, int count) where E : IMessageEnvelope;
//        void EnvelopeMessageWithInnerMessage<E, T>(Stream serialisationStream, E empyEnvelope, T payload) where E : IMessageEnvelope;
//        byte[] Serialize<T>(T record);
//        byte[] SerializeMessageEnvelope<E>(E message) where E : IMessageEnvelope;
//        byte[] SerializeMessageEnvelope<E, T>(E empyEnvelope, T payload) where E : IMessageEnvelope;
//        T UnpackEnvelopedMessage<E, T>(E fullEnvelope) where E : IMessageEnvelope;

//    }
//}



using MessageProtocol;
using NetworkLibrary.Components;
using System.IO;

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

