using NetworkLibrary.TCP.ByteMessage;
using ProtoBuf;
using ProtoBuf.Serializers;
using Protobuff;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Protobuff
{
    public class ProtoServer
    {
        public delegate void MessageReceived(in Guid clientId, MessageEnvelope message);
        public MessageReceived OnMessageReceived;


        private ByteMessageTcpServer server;
        ConcurrentProtoSerialiser serialiser = new ConcurrentProtoSerialiser();
        private MessageAwaiter awaiter;
        
        public ProtoServer(int port, int maxClients)
        {
            server = new ByteMessageTcpServer(port,maxClients);
            awaiter = new MessageAwaiter();

            server.OnClientAccepted += OnClientConnected;
            server.OnBytesReceived += OnBytesReceived;

            server.MaxIndexedMemoryPerClient = 1280000000;
            server.StartServer();

        }

        private void OnClientConnected(Guid guid)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(in Guid clientId, MessageEnvelope message)
        {
            var bytes = serialiser.SerialiseEnvelopedMessage(message);
            server.SendBytesToClient(clientId, bytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(in Guid clientId, MessageEnvelope message, byte[] buffer, int offset, int count)
        {
            byte[] bytes = serialiser.EnvelopeAndSerialiseMessage(message, buffer, offset, count);
            server.SendBytesToClient(clientId, bytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage<T>(in Guid clientId, MessageEnvelope message, T payload) where T : class
        {
            byte[] bytes = serialiser.EnvelopeAndSerialiseMessage(message, payload);
            server.SendBytesToClient(clientId, bytes);

        }

       
        public async Task<MessageEnvelope> SendMessageAndWaitResponse<T>(Guid clientId, MessageEnvelope message, byte[] buffer, int offset, int count, int timeoutMs = 10000)
        {
            var result = awaiter.RegisterWait(message.MessageId, timeoutMs);

            SendAsyncMessage(clientId, message, buffer, offset, count);
            return await result;
        }

        public async Task<MessageEnvelope> SendMessageAndWaitResponse<T>(Guid clientId, MessageEnvelope message, T payload, int timeoutMs = 10000) where T : class
        {
            var result = awaiter.RegisterWait(message.MessageId, timeoutMs);

            SendAsyncMessage(clientId, message, payload);
            return await result;
        }


        public async Task<MessageEnvelope> SendMessageAndWaitResponse(Guid clientId, MessageEnvelope message, int timeoutMs = 10000)
        {
            var result = awaiter.RegisterWait(message.MessageId, timeoutMs);

            SendAsyncMessage(clientId, message);
            return await result;
        }

       

        private void OnBytesReceived(in Guid guid, byte[] bytes, int offset, int count)
        {
            //MessageEnvelope message = serialiser.Deserialize<MessageEnvelope>(bytes, offset, count);
            MessageEnvelope message = serialiser.DeserialiseEnvelopedMessage(bytes, offset, count);

           
               

                    if (awaiter.IsWaiting(message.MessageId))
                        awaiter.ResponseArrived(message);
                    else
                        OnMessageReceived?.Invoke(in guid, message);
           


        }

    }
}
