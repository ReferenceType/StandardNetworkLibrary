using NetworkLibrary.Components;
using NetworkLibrary.MessageProtocol;
using NetworkLibrary.UDP;
using NetworkLibrary.Utils;
using System;
using System.Net;

namespace NetworkLibrary.P2P.Components.Modules
{
    internal class ClientUdpModule<S> : AsyncUdpServerLite where S : ISerializer, new()
    {
        [ThreadStatic]
        private static byte[] TLSBuffer;
        [ThreadStatic]
        private static PooledMemoryStream TLSSerialisationStream;

        private SharerdMemoryStreamPool streamPool = new SharerdMemoryStreamPool();
        private GenericMessageSerializer<S> serialiser = new GenericMessageSerializer<S>();

        public ClientUdpModule(int port) : base(port) { }
        public int MaxUdpPackageSize = 62400;
        private static void GetTLSStream()
        {
            if (TLSSerialisationStream == null)
                TLSSerialisationStream = new PooledMemoryStream();
        }
        private static void GetTlsbuffer()
        {
            if (TLSBuffer == null)
                TLSBuffer = ByteCopy.GetNewArray(65000, true);
        }

        public bool TrySendAsync(IPEndPoint endpoint, MessageEnvelope message, out PooledMemoryStream streamWithLargeMessage)
        {
            GetTLSStream();
            TLSSerialisationStream.Position32 = 0;

            streamWithLargeMessage = null;

            serialiser.EnvelopeMessageWithBytes(TLSSerialisationStream,
                message, message.Payload, message.PayloadOffset, message.PayloadCount);
            if (TLSSerialisationStream.Position32 > MaxUdpPackageSize)
            {
                streamWithLargeMessage = TLSSerialisationStream;
                return false;
            }

            SendBytesToClient(endpoint, TLSSerialisationStream.GetBuffer(), 0, (int)TLSSerialisationStream.Position);
            return true;
        }

        public bool TrySendAsync<T>(IPEndPoint endpoint, MessageEnvelope message, T innerMessage, out PooledMemoryStream excessStream)
        {
            GetTLSStream();
            TLSSerialisationStream.Position32 = 0;
            excessStream = null;

            serialiser.EnvelopeMessageWithInnerMessage(TLSSerialisationStream,
                message, innerMessage);

            if (TLSSerialisationStream.Position32 > MaxUdpPackageSize)
            {
                excessStream = TLSSerialisationStream;
                return false;
            }

            SendBytesToClient(endpoint, TLSSerialisationStream.GetBuffer(), 0, (int)TLSSerialisationStream.Position);
            return true;
        }

        public bool TrySendAsync(IPEndPoint endpoint, MessageEnvelope message, Action<PooledMemoryStream> SerializationCallback, out PooledMemoryStream excessStream)
        {
            GetTLSStream();
            TLSSerialisationStream.Position32 = 0;

            serialiser.EnvelopeMessageWithInnerMessage(TLSSerialisationStream,
                message, SerializationCallback);

            excessStream = null;
            if (TLSSerialisationStream.Position32 > MaxUdpPackageSize)
            {
                excessStream = TLSSerialisationStream;
                return false;
            }

            SendBytesToClient(endpoint, TLSSerialisationStream.GetBuffer(), 0, TLSSerialisationStream.Position32);
            return true;

        }

        public bool TrySendAsync(IPEndPoint endpoint, MessageEnvelope message, ConcurrentAesAlgorithm algorithm, out PooledMemoryStream largeMessageStream)
        {
            GetTLSStream();
            TLSSerialisationStream.Position32 = 0;

            largeMessageStream = null;

            serialiser.EnvelopeMessageWithBytes(TLSSerialisationStream,
                message, message.Payload, message.PayloadOffset, message.PayloadCount);

            if (TLSSerialisationStream.Position32 > MaxUdpPackageSize)
            {
                largeMessageStream = TLSSerialisationStream;
                return false;
            }

            GetTlsbuffer();
            int amountEncypted = algorithm.EncryptInto(TLSSerialisationStream.GetBuffer(), 0, TLSSerialisationStream.Position32, TLSBuffer, 0);

            SendBytesToClient(endpoint, TLSBuffer, 0, amountEncypted);
            return true;
        }

        public bool TrySendAsync(IPEndPoint endpoint, MessageEnvelope message, Action<PooledMemoryStream> SerializationCallback, ConcurrentAesAlgorithm algorithm, out PooledMemoryStream excessStream)
        {
            GetTLSStream();
            TLSSerialisationStream.Position32 = 0;

            serialiser.EnvelopeMessageWithInnerMessage(TLSSerialisationStream,
                message, SerializationCallback);

            excessStream = null;
            if (TLSSerialisationStream.Position32 > MaxUdpPackageSize)
            {
                excessStream = TLSSerialisationStream;
                return false;
            }

            GetTlsbuffer();
            int amountEncypted = algorithm.EncryptInto(TLSSerialisationStream.GetBuffer(), 0, TLSSerialisationStream.Position32, TLSBuffer, 0);

            SendBytesToClient(endpoint, TLSBuffer, 0, amountEncypted);
            return true;
        }

        public bool TrySendAsync<T>(IPEndPoint endpoint, MessageEnvelope message, T innerMessage, ConcurrentAesAlgorithm algorithm, out PooledMemoryStream excessStream)
        {
            GetTLSStream();
            TLSSerialisationStream.Position32 = 0;

            excessStream = null;

            serialiser.EnvelopeMessageWithInnerMessage(TLSSerialisationStream,
                message, innerMessage);
            if (TLSSerialisationStream.Position32 > MaxUdpPackageSize)
            {
                excessStream = TLSSerialisationStream;
                return false;
            }

            GetTlsbuffer();
            int amountEncypted = algorithm.EncryptInto(TLSSerialisationStream.GetBuffer(), 0, TLSSerialisationStream.Position32, TLSBuffer, 0);

            SendBytesToClient(endpoint, TLSBuffer, 0, amountEncypted);
            return true;
        }

       
    }
}
