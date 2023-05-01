using MessageProtocol;
using NetworkLibrary.Components;
using NetworkLibrary.MessageProtocol;
using NetworkLibrary.UDP.Secure;
using NetworkLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Protobuff
{
    public class GenericSecureUdpMessageClient<E,S> : SecureUdpClient 
        where E:IMessageEnvelope,new()
        where S: ISerializer,new()
    {
        public Action<E> OnMessageReceived;
        private readonly GenericMessageSerializer<E, S> serialiser = new GenericMessageSerializer<E, S>();
        private readonly SharerdMemoryStreamPool streamPool = new SharerdMemoryStreamPool();

        public GenericSecureUdpMessageClient(ConcurrentAesAlgorithm algorithm) : base(algorithm) { }

        protected override void HandleDecrypedBytes(byte[] buffer, int offset, int count)
        {
            var msg = serialiser.DeserialiseEnvelopedMessage(buffer, offset, count);
            OnMessageReceived?.Invoke(msg);
        }

        public void SendAsyncMessage(E message)
        {
            var serialisationStream = streamPool.RentStream();
            serialiser.EnvelopeMessageWithBytes(serialisationStream,
                message, message.Payload, message.PayloadOffset, message.PayloadCount);

            SendAsync(serialisationStream.GetBuffer(), 0, (int)serialisationStream.Position);
            streamPool.ReturnStream(serialisationStream);
        }

        public void SendAsyncMessage<T>(E message, T innerMessage) 
        {
            var serialisationStream = streamPool.RentStream();
            serialiser.EnvelopeMessageWithInnerMessage(serialisationStream, message, innerMessage);

            SendAsync(serialisationStream.GetBuffer(), 0, (int)serialisationStream.Position);
            streamPool.ReturnStream(serialisationStream);
        }

        public void SendAsyncMessage(E message, byte[] payload, int offset, int count)
        {
            var serialisationStream = streamPool.RentStream();
            serialiser.EnvelopeMessageWithBytes(serialisationStream, message, payload, offset, count);

            SendAsync(serialisationStream.GetBuffer(), 0, (int)serialisationStream.Position);
            streamPool.ReturnStream(serialisationStream);
        }
    }
}
