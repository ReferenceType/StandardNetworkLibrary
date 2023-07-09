using NetworkLibrary;
using NetworkLibrary.Components;
using NetworkLibrary.UDP;
using NetworkLibrary.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Protobuff.P2P.Generic.Modules
{
    internal class ClientUdpModule : AsyncUdpServerLite
    {
        [ThreadStatic]
        private static byte[] TLSBuffer;
        [ThreadStatic]
        private static PooledMemoryStream TLSSerialisationStream;

        private SharerdMemoryStreamPool streamPool = new SharerdMemoryStreamPool();
        private ConcurrentProtoSerialiser serialiser = new ConcurrentProtoSerialiser();

        public ClientUdpModule(int port) : base(port) { }

        private static PooledMemoryStream GetTLSStream()
        {
            if (TLSSerialisationStream == null)
                TLSSerialisationStream = new PooledMemoryStream();
            return TLSSerialisationStream;
        }

        public bool TrySendAsync(IPEndPoint endpoint, MessageEnvelope message, out PooledMemoryStream streamWithLargeMessage)
        {
            if (TLSSerialisationStream == null)
                TLSSerialisationStream = new PooledMemoryStream();
            TLSSerialisationStream.Position32 = 0;

            streamWithLargeMessage = TLSSerialisationStream;

            serialiser.EnvelopeMessageWithBytes(TLSSerialisationStream,
                message, message.Payload, message.PayloadOffset, message.PayloadCount);
            if (TLSSerialisationStream.Position32 > 64500)
            {
                return false;
            }

            SendBytesToClient(endpoint, TLSSerialisationStream.GetBuffer(), 0, (int)TLSSerialisationStream.Position);
            return true;
        }

        public bool TrySendAsync<T>(IPEndPoint endpoint, MessageEnvelope message, T innerMessage, out PooledMemoryStream excessStream)
        {
            if (TLSSerialisationStream == null)
                TLSSerialisationStream = new PooledMemoryStream();
            TLSSerialisationStream.Position32 = 0;

            serialiser.EnvelopeMessageWithInnerMessage(TLSSerialisationStream,
                message, innerMessage);
            excessStream = TLSSerialisationStream;
            if (TLSSerialisationStream.Position32 > 64500)
            {
                return false;
            }

            SendBytesToClient(endpoint, TLSSerialisationStream.GetBuffer(), 0, (int)TLSSerialisationStream.Position);
            return true;
        }

        public bool TrySendAsync(IPEndPoint endpoint, MessageEnvelope message, Action<PooledMemoryStream> SerializationCallback, out PooledMemoryStream excessStream)
        {
            if (TLSSerialisationStream == null)
                TLSSerialisationStream = new PooledMemoryStream();
            TLSSerialisationStream.Position32 = 0;

            serialiser.EnvelopeMessageWithInnerMessage(TLSSerialisationStream,
                message, SerializationCallback);

            excessStream = TLSSerialisationStream;
            if (TLSSerialisationStream.Position32 > 64500)
            {
                return false;
            }

            SendBytesToClient(endpoint, TLSSerialisationStream.GetBuffer(), 0, TLSSerialisationStream.Position32);
            return true;

        }

        public bool TrySendAsync(IPEndPoint endpoint, MessageEnvelope message, ConcurrentAesAlgorithm algorithm, out PooledMemoryStream largeMessageStream)
        {
            if (TLSSerialisationStream == null)
                TLSSerialisationStream = new PooledMemoryStream();
            TLSSerialisationStream.Position32 = 0;

            largeMessageStream = TLSSerialisationStream;

            serialiser.EnvelopeMessageWithBytes(TLSSerialisationStream,
                message, message.Payload, message.PayloadOffset, message.PayloadCount);

            if (TLSSerialisationStream.Position32 > 64500)
            {
                return false;
            }

            if (TLSBuffer == null)
                TLSBuffer = ByteCopy.GetNewArray(65000, true);
            int amountEncypted = algorithm.EncryptInto(TLSSerialisationStream.GetBuffer(), 0, TLSSerialisationStream.Position32, TLSBuffer, 0);

            SendBytesToClient(endpoint, TLSBuffer, 0, amountEncypted);
            return true;
        }

        public bool TrySendAsync(IPEndPoint endpoint, MessageEnvelope message, Action<PooledMemoryStream> SerializationCallback, ConcurrentAesAlgorithm algorithm, out PooledMemoryStream excessStream)
        {
            if (TLSSerialisationStream == null)
                TLSSerialisationStream = new PooledMemoryStream();
            TLSSerialisationStream.Position32 = 0;

            serialiser.EnvelopeMessageWithInnerMessage(TLSSerialisationStream,
                message, SerializationCallback);
            excessStream = TLSSerialisationStream;
            if (TLSSerialisationStream.Position32 > 64500)
            {
                return false;
            }

            if (TLSBuffer == null)
                TLSBuffer = ByteCopy.GetNewArray(65000, true);

            int amountEncypted = algorithm.EncryptInto(TLSSerialisationStream.GetBuffer(), 0, TLSSerialisationStream.Position32, TLSBuffer, 0);

            SendBytesToClient(endpoint, TLSBuffer, 0, amountEncypted);
            return true;
        }

        public bool TrySendAsync<T>(IPEndPoint endpoint, MessageEnvelope message, T innerMessage, ConcurrentAesAlgorithm algorithm, out PooledMemoryStream excessStream)
        {
            if (TLSSerialisationStream == null)
                TLSSerialisationStream = new PooledMemoryStream();
            TLSSerialisationStream.Position32 = 0;

            excessStream = TLSSerialisationStream;

            serialiser.EnvelopeMessageWithInnerMessage(TLSSerialisationStream,
                message, innerMessage);
            if (TLSSerialisationStream.Position32 > 64500)
            {
                return false;
            }
            if (TLSBuffer == null)
                TLSBuffer = ByteCopy.GetNewArray(65000, true);
            int amountEncypted = algorithm.EncryptInto(TLSSerialisationStream.GetBuffer(), 0, TLSSerialisationStream.Position32, TLSBuffer, 0);

            SendBytesToClient(endpoint, TLSBuffer, 0, amountEncypted);
            return true;
        }


    }
}
