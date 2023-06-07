using NetworkLibrary;
using NetworkLibrary.Components;
using NetworkLibrary.UDP;
using NetworkLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;

namespace Protobuff
{
    public class UdpMessageServer:AsyncUdpServerLite
    {
        private SharerdMemoryStreamPool streamPool =  new SharerdMemoryStreamPool();
        private ConcurrentProtoSerialiser serialiser =  new ConcurrentProtoSerialiser();

        public UdpMessageServer(int port) :base(port) { }

        public void SendAsync(IPEndPoint endpoint, MessageEnvelope message)
        {
            var serialisationStream = streamPool.RentStream();
            serialiser.EnvelopeMessageWithBytes(serialisationStream,
                message, message.Payload, message.PayloadOffset, message.PayloadCount);

            SendBytesToClient(endpoint, serialisationStream.GetBuffer(), 0, (int)serialisationStream.Position);
            streamPool.ReturnStream(serialisationStream);
        }

        public void SendAsync<T>(IPEndPoint endpoint, MessageEnvelope message, T innerMessage)
        {
            var serialisationStream = streamPool.RentStream();
            serialiser.EnvelopeMessageWithInnerMessage(serialisationStream,
                message, innerMessage);

            SendBytesToClient(endpoint, serialisationStream.GetBuffer(), 0, (int)serialisationStream.Position);
            streamPool.ReturnStream(serialisationStream);
        }

        public void SendAsync(IPEndPoint endpoint, MessageEnvelope message, Action<PooledMemoryStream> SerializationCallback)
        {
            var serialisationStream = streamPool.RentStream();
            serialiser.EnvelopeMessageWithInnerMessage(serialisationStream,
                message, SerializationCallback);

            SendBytesToClient(endpoint, serialisationStream.GetBuffer(), 0, serialisationStream.Position32);
            streamPool.ReturnStream(serialisationStream);
        }
        public void SendAsync(IPEndPoint endpoint, MessageEnvelope message, ConcurrentAesAlgorithm algorithm)
        {
            var serialisationStream = streamPool.RentStream();

            serialiser.EnvelopeMessageWithBytes(serialisationStream,
                message, message.Payload, message.PayloadOffset, message.PayloadCount);

            var buffer = BufferPool.RentBuffer(serialisationStream.Position32 + 256);
            int amountEncypted = algorithm.EncryptInto(serialisationStream.GetBuffer(), 0, serialisationStream.Position32, buffer, 0);

            SendBytesToClient(endpoint, buffer, 0, amountEncypted);
            streamPool.ReturnStream(serialisationStream);
            BufferPool.ReturnBuffer(buffer);
        }
        public void SendAsync(IPEndPoint endpoint, MessageEnvelope message,
            Action<PooledMemoryStream>SerializationCallback, ConcurrentAesAlgorithm algorithm, byte opCode = 0)
        {
            var serialisationStream = streamPool.RentStream();

            serialiser.EnvelopeMessageWithInnerMessage(serialisationStream,
                message, SerializationCallback);

            var buffer = BufferPool.RentBuffer(serialisationStream.Position32 + 256);
            int amountEncypted = algorithm.EncryptInto(serialisationStream.GetBuffer(), 0, serialisationStream.Position32, buffer, 0);

            SendBytesToClient(endpoint, buffer, 0, amountEncypted);
            streamPool.ReturnStream(serialisationStream);
            BufferPool.ReturnBuffer(buffer);
        }

        public void SendAsync<T>(IPEndPoint endpoint, MessageEnvelope message, T innerMessage, ConcurrentAesAlgorithm algorithm)
        {
            var serialisationStream = streamPool.RentStream();
            serialiser.EnvelopeMessageWithInnerMessage(serialisationStream,
                message, innerMessage);

            var buffer = BufferPool.RentBuffer(serialisationStream.Position32+256);
            int amountEncypted = algorithm.EncryptInto(serialisationStream.GetBuffer(), 0, serialisationStream.Position32, buffer, 0);

            SendBytesToClient(endpoint, buffer,0,amountEncypted);
            streamPool.ReturnStream(serialisationStream);
            BufferPool.ReturnBuffer(buffer);
        }
   
    }
}
