using NetworkLibrary.Components;
using NetworkLibrary.UDP.Secure;
using NetworkLibrary.Utils;
using ProtoBuf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace Protobuff
{
    internal class EncryptedUdpProtoClient:SecureUdpClient
    {
        //SecureUdpClient encryptedUdpClient;
        public Action<MessageEnvelope> OnMessageReceived;
        ConcurrentProtoSerialiser serialiser =  new ConcurrentProtoSerialiser();
        MemoryStreamPool streamPool = new MemoryStreamPool();

        public EncryptedUdpProtoClient(ConcurrentAesAlgorithm algorithm):base(algorithm)
        {

        }

       
        protected override void HandleDecrypedBytes(byte[] buffer, int offset, int count)
        {
            var msg = serialiser.DeserialiseEnvelopedMessage(buffer,offset,count);
            OnMessageReceived?.Invoke(msg);
            // base.HandleBytesReceived(buffer, offset, count);
        }

        public void SendAsyncMessage(MessageEnvelope message)
        {
            var serialisationStream = streamPool.RentStream();

            if (message.Payload == null)
                serialiser.EnvelopeMessageWithBytes(serialisationStream, message, null, 0, 0);
            else
                serialiser.EnvelopeMessageWithBytes(serialisationStream, message, message.Payload, 0, message.Payload.Length);

            SendAsync(serialisationStream.GetBuffer(), 0, (int)serialisationStream.Position);

            streamPool.ReturnStream(serialisationStream);
        }
        public void SendAsyncMessage<T>(MessageEnvelope message, T innerMessage) where T : class
        {
            var serialisationStream = streamPool.RentStream();

            serialiser.EnvelopeMessageWithInnerMessage(serialisationStream,message, innerMessage);
            SendAsync(serialisationStream.GetBuffer(), 0, (int)serialisationStream.Position);

            streamPool.ReturnStream(serialisationStream);


        }
        public void SendAsyncMessage(MessageEnvelope message, byte[] payload ,int offset, int count)
        {
            var serialisationStream = streamPool.RentStream();

            serialiser.EnvelopeMessageWithBytes(serialisationStream, message, payload,offset,count);
            SendAsync(serialisationStream.GetBuffer(), 0, (int)serialisationStream.Position);

            streamPool.ReturnStream(serialisationStream);

        }
    }
}
