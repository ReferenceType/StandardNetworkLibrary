using NetworkLibrary;
using NetworkLibrary.Components;
using NetworkLibrary.UDP;
using NetworkLibrary.Utils;
using Protobuff.P2P.StateManagemet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Protobuff.P2P.Modules
{
    internal class ClientUdpModule : AsyncUdpServerLite
    {

        private SharerdMemoryStreamPool streamPool = new SharerdMemoryStreamPool();
        private ConcurrentProtoSerialiser serialiser = new ConcurrentProtoSerialiser();

        public ClientUdpModule(int port) : base(port) { }

        internal void SendAsync(IPEndPoint endpoint, byte[] bytes, int offset, int count, ConcurrentAesAlgorithm algorithm)
        {
            var buffer = BufferPool.RentBuffer(count + 256);
            int amountEncypted = algorithm.EncryptInto(bytes,offset,count, buffer, 0);

            SendBytesToClient(endpoint, buffer, 0, amountEncypted);
            BufferPool.ReturnBuffer(buffer);
        }

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
            int amountEncypted = algorithm.EncryptInto(serialisationStream.GetBuffer(), 0, serialisationStream.Position32 , buffer, 0);

            SendBytesToClient(endpoint, buffer, 0, amountEncypted );
            streamPool.ReturnStream(serialisationStream);
            BufferPool.ReturnBuffer(buffer);
        }
        public void SendAsync(IPEndPoint endpoint, MessageEnvelope message, Action<PooledMemoryStream> SerializationCallback, ConcurrentAesAlgorithm algorithm)
        {
            var serialisationStream = streamPool.RentStream();

            serialiser.EnvelopeMessageWithInnerMessage(serialisationStream,
                message, SerializationCallback);

            var buffer = BufferPool.RentBuffer(serialisationStream.Position32 + 256);
            int amountEncypted = algorithm.EncryptInto(serialisationStream.GetBuffer(), 0, serialisationStream.Position32, buffer, 0);

            SendBytesToClient(endpoint, buffer, 0, amountEncypted );
            streamPool.ReturnStream(serialisationStream);
            BufferPool.ReturnBuffer(buffer);
        }

        public void SendAsync<T>(IPEndPoint endpoint, MessageEnvelope message, T innerMessage, ConcurrentAesAlgorithm algorithm)
        {
            var serialisationStream = streamPool.RentStream();

            serialiser.EnvelopeMessageWithInnerMessage(serialisationStream,
                message, innerMessage);

            var buffer = BufferPool.RentBuffer(serialisationStream.Position32 + 256);
            int amountEncypted = algorithm.EncryptInto(serialisationStream.GetBuffer(), 0, serialisationStream.Position32, buffer, 0);

            SendBytesToClient(endpoint, buffer, 0, amountEncypted );
            streamPool.ReturnStream(serialisationStream);
            BufferPool.ReturnBuffer(buffer);
        }


    }
}
