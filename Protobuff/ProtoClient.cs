using NetworkLibrary.Components;
using NetworkLibrary.TCP.Base;
using NetworkLibrary.TCP.ByteMessage;
using NetworkLibrary.Utils;
using ProtoBuf;
using Protobuff;
using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading.Tasks;

namespace Protobuff
{
    public class ProtoClient
    {
        private ByteMessageTcpClient client;

        public Action<MessageEnvelope> OnMessageReceived;

        private ConcurrentProtoSerialiser serialiser;
        private MessageAwaiter awaiter;
        private SharerdMemoryStreamPool streamPool = new SharerdMemoryStreamPool();

        public ProtoClient()
        {
            client = new ByteMessageTcpClient();
            client.OnBytesReceived += BytesReceived;
            client.OnDisconnected += Disconnected;
            client.OnConnected += Connected;
            client.MaxIndexedMemory = 1280000000;
            client.GatherConfig = ScatterGatherConfig.UseBuffer;

            serialiser = new ConcurrentProtoSerialiser();
            awaiter = new MessageAwaiter();
        }
        private PooledMemoryStream RentStream()
        {
            return streamPool.RentStream();
        }
        private void ReturnStream(PooledMemoryStream stream)
        {
            streamPool.ReturnStream(stream);
        }
        public void Connect(string host, int port)
        {
            client.Connect(host, port);
        }

        private void Connected()
        {
        }

        private void Disconnected()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(MessageEnvelope message)
        {
            var stream = RentStream();

            if (message.Payload != null)
                serialiser.EnvelopeMessageWithBytes(stream, message, message.Payload, 0, message.Payload.Length);
            else
                serialiser.EnvelopeMessageWithBytes(stream, message, null, 0, 0);

            client.SendAsync(stream.GetBuffer(), 0, (int)stream.Position);
            ReturnStream(stream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(MessageEnvelope message, byte[] buffer, int offset, int count)
        {
            var stream = RentStream();
            serialiser.EnvelopeMessageWithBytes(stream, message, buffer, offset, count);
            client.SendAsync(stream.GetBuffer(), 0, (int)stream.Position);
            ReturnStream(stream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage<T>(MessageEnvelope message, T payload) where T : IProtoMessage
        {
            var stream = RentStream();
            serialiser.EnvelopeMessageWithInnerMessage(stream, message, payload);
            client.SendAsync(stream.GetBuffer(), 0, (int)stream.Position);
            ReturnStream(stream);
        }

        public async Task<MessageEnvelope> SendMessageAndWaitResponse(MessageEnvelope message, int timeoutMs = 10000)
        {
            message.MessageId = Guid.NewGuid();

            var result = awaiter.RegisterWait(message.MessageId, timeoutMs);

            SendAsyncMessage(message);
            return await result;
        }

        public async Task<MessageEnvelope> SendMessageAndWaitResponse<T>(MessageEnvelope message, T payload, int timeoutMs = 10000) where T : IProtoMessage
        {
            message.MessageId = Guid.NewGuid();

            var result = awaiter.RegisterWait(message.MessageId, timeoutMs);

            SendAsyncMessage(message, payload);
            return await result;
        }

        public async Task<MessageEnvelope> SendMessageAndWaitResponse(MessageEnvelope message, byte[] buffer, int offset, int count, int timeoutMs = 10000)
        {
            message.MessageId = Guid.NewGuid();

            var result = awaiter.RegisterWait(message.MessageId, timeoutMs);

            SendAsyncMessage(message, buffer, offset, count);
            return await result;
        }

        private void BytesReceived(byte[] bytes, int offset, int count)
        {
            MessageEnvelope message = serialiser.DeserialiseEnvelopedMessage(bytes, offset, count);

            if(awaiter.IsWaiting(message.MessageId))
                awaiter.ResponseArrived(message);
            else
                OnMessageReceived?.Invoke(message);
           
        }


    }
}
