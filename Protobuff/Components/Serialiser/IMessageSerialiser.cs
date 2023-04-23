//using Protobuff.Components.TransportWrapper.Generic.Interfaces;
//using System.IO;

//namespace Protobuff.Components.Serialiser
//{
//    public interface IMessageSerialiser
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