using System.IO;



internal interface IMessageSerialiser
{
    IMessageEnvelope DeserialiseEnvelopedMessage(byte[] buffer, int offset, int count);
    IRouterHeader DeserialiseOnlyRouterHeader(byte[] buffer, int offset, int count);
    T Deserialize<T>(byte[] data);
    T Deserialize<T>(byte[] data, int offset, int count);
    byte[] EnvelopeMessageWithBytes(IMessageEnvelope empyEnvelope, byte[] payloadBuffer, int offset, int count);
    void EnvelopeMessageWithBytes(Stream serialisationStream, IMessageEnvelope empyEnvelope, byte[] payloadBuffer, int offset, int count);
    void EnvelopeMessageWithInnerMessage<T>(Stream serialisationStream, IMessageEnvelope empyEnvelope, T payload);
    byte[] Serialize<T>(T record);
    bool SerializeInto<T>(T record, ref byte[] buffer, int offset, out int count);
    byte[] SerializeMessageEnvelope(IMessageEnvelope message);
    byte[] SerializeMessageEnvelope<T>(IMessageEnvelope empyEnvelope, T payload);
    T UnpackEnvelopedMessage<T>(IMessageEnvelope fullEnvelope);


}