using NetworkLibrary.Components;
using NetworkLibrary.Components.Crypto;
using NetworkLibrary.MessageProtocol;
using NetworkLibrary.TCP.AES;
using NetworkLibrary.TCP.Base;
using NetworkLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkLibrary.P2P.Components.Modules
{
    internal class AesTcpModule<S> where S :ISerializer, new()
    {
        public Action<MessageEnvelope> OnMessageReceived;

        [ThreadStatic]
        private static PooledMemoryStream TLSSerialisationStream;
      

        private GenericMessageSerializer<S> serialiser = new GenericMessageSerializer<S>();
        readonly bool IsBasedOnServer = false;
        private AesTcpServer server;
        private Guid clientId;
        private AesTcpClient client;
        //private ConcurrentAesAlgorithm encryptor;
        GenericMessageAwaiter<MessageEnvelope> awaiter;
        Guid ownerId;
        Guid destinationId;
        public AesTcpModule(AesTcpServer server, GenericMessageAwaiter<MessageEnvelope> awaiter, Guid sessionId, Guid destinationId)
        {
            ownerId = sessionId;
            this.destinationId = destinationId;
            IsBasedOnServer = true;
            clientId = server.Sessions.First().Key;
            this.server = server;
            this.awaiter = awaiter;
            server.OnBytesReceived += ReceivedS;
          
        }

        public AesTcpModule(AesTcpClient client, GenericMessageAwaiter<MessageEnvelope> awaiter, Guid sessionId, Guid destinationId)
        {
            ownerId = sessionId;
            this.destinationId= destinationId;
            this.client = client;
            this.awaiter = awaiter;
            client.OnBytesReceived += Received;
        }

        private static void GetSerialisationStream()
        {
            if (TLSSerialisationStream == null)
                TLSSerialisationStream = new PooledMemoryStream();
        }
      
        public void SendAsync(MessageEnvelope message)
        {
            message = MessageEnvelope.CloneWithNoRouter(message);


            GetSerialisationStream();
            TLSSerialisationStream.Position32 = 0;
            serialiser.EnvelopeMessageWithBytesDontWritePayload(TLSSerialisationStream,
                message, message.PayloadCount);
            if (message.Payload != null)
                SendAsync(TLSSerialisationStream.GetBuffer(), 0, TLSSerialisationStream.Position32
                    , message.Payload, message.PayloadOffset, message.PayloadCount);
            else
                SendAsync(TLSSerialisationStream.GetBuffer(), 0, TLSSerialisationStream.Position32);

        }

        public void SendAsync<T>(MessageEnvelope message, T innerMsg)
        {
            message = MessageEnvelope.CloneWithNoRouter(message);


            GetSerialisationStream();
            TLSSerialisationStream.Position32 = 0;
            serialiser.EnvelopeMessageWithInnerMessage(TLSSerialisationStream,
                message, innerMsg);

            SendAsync(TLSSerialisationStream.GetBuffer(), 0, TLSSerialisationStream.Position32);
        }
        public void SendAsync(MessageEnvelope message, Action<PooledMemoryStream> serializationCallback)
        {
            message = MessageEnvelope.CloneWithNoRouter(message);


            GetSerialisationStream();
            TLSSerialisationStream.Position32 = 0;
            serialiser.EnvelopeMessageWithInnerMessage(TLSSerialisationStream,
                message, serializationCallback);

            SendAsync(TLSSerialisationStream.GetBuffer(), 0, TLSSerialisationStream.Position32);
        }
        public Task<MessageEnvelope> SendMessageAndWaitResponse<T>(MessageEnvelope message, T payload, int timeout)
        {
            message = MessageEnvelope.CloneWithNoRouter(message);

            message.MessageId = Guid.NewGuid();
            var task = awaiter.RegisterWait(message.MessageId, timeout);

            SendAsync(message, payload);
            return task;
        }
        public Task<MessageEnvelope> SendMessageAndWaitResponse(MessageEnvelope message, int timeout)
        {
            message = MessageEnvelope.CloneWithNoRouter(message);

            message.MessageId = Guid.NewGuid();
            var task = awaiter.RegisterWait(message.MessageId, timeout);

            SendAsync(message);
            return task;
        }
        private void ReceivedS(Guid guid, byte[] bytes, int offset, int count)
        {
           HandleReceivedBytes(bytes, offset, count);
        }
        private void Received(byte[] bytes, int offset, int count)
        {
            HandleReceivedBytes(bytes, offset, count);
        }

        private void HandleReceivedBytes(byte[] bytes, int offset, int count)
        {
            var msg = serialiser.DeserialiseEnvelopedMessage(bytes,offset,count);
            msg.To = ownerId;
            msg.From = destinationId;
            OnMessageReceived?.Invoke(msg);
        }

       
        private void SendAsync(byte[] buffer, int offset, int count)
        {
            if(!IsBasedOnServer)
            {
                client.SendAsync(buffer, offset, count);
            }
            else
            {
                server.SendBytesToClient(clientId, buffer, offset, count);
            }
        }
        private void SendAsync(byte[] buffer, int offset, int count, byte[] buffer2, int offset2, int count2)
        {
            if (!IsBasedOnServer)
            {
                client.SendAsync(buffer, offset, count, buffer2, offset2, count2);
            }
            else
            {
                server.SendBytesToClient(clientId, buffer, offset, count, buffer2, offset2, count2);
            }
        }

        internal void Dispose()
        {
           OnMessageReceived = null;
           client?.Dispose();
           server?.ShutdownServer();
        }
    }
}
