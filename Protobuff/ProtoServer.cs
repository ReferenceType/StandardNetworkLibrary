using NetworkLibrary.Components;
using NetworkLibrary.TCP.ByteMessage;
using NetworkLibrary.Utils;
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
        private ConcurrentProtoSerialiser serialiser = new ConcurrentProtoSerialiser();
        private MessageAwaiter awaiter;
        private SharerdMemoryStreamPool streamPool = new SharerdMemoryStreamPool();

        public ProtoServer(int port, int maxClients)
        {
            server = new ByteMessageTcpServer(port);
            awaiter = new MessageAwaiter();

            server.OnClientAccepted += OnClientConnected;
            server.OnBytesReceived += OnBytesReceived;
            server.GatherConfig = ScatterGatherConfig.UseBuffer;

            server.MaxIndexedMemoryPerClient = 1280000000;
            server.StartServer();

        }
        private PooledMemoryStream RentStream()
        {
            return streamPool.RentStream();
        }
        private void ReturnStream(PooledMemoryStream stream)
        {
            streamPool.ReturnStream(stream);
        }
        private void OnClientConnected(Guid guid)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(in Guid clientId, MessageEnvelope message)
        {
            var stream = RentStream();
            if (message.Payload != null)
                serialiser.EnvelopeMessageWithBytes(stream, message, message.Payload, 0, message.Payload.Length);
            else
                serialiser.EnvelopeMessageWithBytes(stream, message, null, 0, 0);

            server.SendBytesToClient(clientId, stream.GetBuffer(), 0, (int)stream.Position);
            ReturnStream(stream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(in Guid clientId, MessageEnvelope message, byte[] buffer, int offset, int count)
        {
            var stream = RentStream();

            serialiser.EnvelopeMessageWithBytes(stream, message, buffer, offset, count);

            server.SendBytesToClient(clientId, stream.GetBuffer(), 0, (int)stream.Position);
            ReturnStream(stream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage<T>(in Guid clientId, MessageEnvelope message, T payload) where T : IProtoMessage
        {
            var stream = RentStream();

            serialiser.EnvelopeMessageWithInnerMessage(stream, message, payload);

            server.SendBytesToClient(clientId, stream.GetBuffer(), 0, (int)stream.Position);
            ReturnStream(stream);
        }

        public async Task<MessageEnvelope> SendMessageAndWaitResponse<T>(Guid clientId, MessageEnvelope message, byte[] buffer, int offset, int count, int timeoutMs = 10000)
        {
            var result = awaiter.RegisterWait(message.MessageId, timeoutMs);
            message.MessageId = Guid.NewGuid();

            SendAsyncMessage(clientId, message, buffer, offset, count);
            return await result;
        }

        public async Task<MessageEnvelope> SendMessageAndWaitResponse<T>(Guid clientId, MessageEnvelope message, T payload, int timeoutMs = 10000) where T : IProtoMessage
        {
            var result = awaiter.RegisterWait(message.MessageId, timeoutMs);
            message.MessageId = Guid.NewGuid();

            SendAsyncMessage(clientId, message, payload);
            return await result;
        }


        public async Task<MessageEnvelope> SendMessageAndWaitResponse(Guid clientId, MessageEnvelope message, int timeoutMs = 10000)
        {
            message.MessageId = Guid.NewGuid();
            var result = awaiter.RegisterWait(message.MessageId, timeoutMs);

            SendAsyncMessage(clientId, message);
            return await result;
        }



        private void OnBytesReceived(in Guid guid, byte[] bytes, int offset, int count)
        {
            MessageEnvelope message = serialiser.DeserialiseEnvelopedMessage(bytes, offset, count);

            if (awaiter.IsWaiting(message.MessageId))
                awaiter.ResponseArrived(message);
            else
                OnMessageReceived?.Invoke(in guid, message);

        }

    }
}
