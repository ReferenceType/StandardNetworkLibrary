using NetworkLibrary.Components;
using NetworkLibrary.MessageProtocol;
using NetworkLibrary.P2P.Generic;
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
        public int MaxUdpPackageSize = 62400;
        RelayClientBase<S> associatedClient;
        public ClientUdpModule(int port, RelayClientBase<S> associatedClient) : base(port) 
        { 
            this.associatedClient = associatedClient;
        }
        

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
            if(endpoint!= associatedClient.relayServerEndpoint)
            {
                message = MessageEnvelope.CloneWithNoRouter(message);
            }

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
            if (endpoint != associatedClient.relayServerEndpoint)
            {
                message = MessageEnvelope.CloneWithNoRouter(message);
            }
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

        public bool TrySendAsync(IPEndPoint endpoint, MessageEnvelope message, 
            Action<PooledMemoryStream> SerializationCallback, out PooledMemoryStream excessStream, bool forceRouterHeader = true)
        {
            if (!forceRouterHeader&&endpoint != associatedClient.relayServerEndpoint)
            {
                message = MessageEnvelope.CloneWithNoRouter(message);
            }
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
            if (endpoint != associatedClient.relayServerEndpoint)
            {
                message = MessageEnvelope.CloneWithNoRouter(message);
            }
            GetTLSStream();
            TLSSerialisationStream.Position32 = 0;

            largeMessageStream = null;

            //serialiser.EnvelopeMessageWithBytes(TLSSerialisationStream,
            //    message, message.Payload, message.PayloadOffset, message.PayloadCount);
            // dont write payload here. if count exist then write

            serialiser.EnvelopeMessageWithBytesDontWritePayload(TLSSerialisationStream, message, message.PayloadCount);

            if (TLSSerialisationStream.Position32+message.PayloadCount > MaxUdpPackageSize)
            {
                TLSSerialisationStream.Write(message.Payload,message.PayloadOffset,message.PayloadCount);
                largeMessageStream = TLSSerialisationStream;
                return false;
            }

            GetTlsbuffer();
            int amountEncypted = 0;
            if (message.Payload != null)
            {
                 amountEncypted = algorithm.EncryptInto(TLSSerialisationStream.GetBuffer(), 0, TLSSerialisationStream.Position32,
               message.Payload, message.PayloadOffset, message.PayloadCount,
               TLSBuffer, 0);
            }
            else
            {
                 amountEncypted = algorithm.EncryptInto(TLSSerialisationStream.GetBuffer(), 0, TLSSerialisationStream.Position32,
              TLSBuffer, 0);
            }
           

            SendBytesToClient(endpoint, TLSBuffer, 0, amountEncypted);
            return true;
        }

        public bool TrySendAsync(IPEndPoint endpoint,
            MessageEnvelope message, 
            Action<PooledMemoryStream> SerializationCallback,
            ConcurrentAesAlgorithm algorithm,
            out PooledMemoryStream excessStream,
            bool forceRouterHeader = false)
        {
            if (!forceRouterHeader&& endpoint != associatedClient.relayServerEndpoint)
            {
                message = MessageEnvelope.CloneWithNoRouter(message);
            }

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
            if (endpoint != associatedClient.relayServerEndpoint)
            {
                message = MessageEnvelope.CloneWithNoRouter(message);

            }

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
