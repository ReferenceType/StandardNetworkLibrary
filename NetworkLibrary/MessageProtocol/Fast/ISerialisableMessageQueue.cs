namespace NetworkLibrary.MessageProtocol
{
    internal interface ISerialisableMessageQueue
    {
        bool TryEnqueueMessage<T>(MessageEnvelope envelope, T message);
        bool TryEnqueueMessage(MessageEnvelope envelope);
    }
}
