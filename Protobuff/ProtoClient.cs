using NetworkLibrary.TCP.Base;
using NetworkLibrary.TCP.ByteMessage;
using ProtoBuf;
using Protobuff;
using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Protobuff
{
    public class ProtoClient
    {
        private ByteMessageTcpClient client;

        public Action<MessageEnvelope> OnMessageReceived;

        private ConcurrentProtoSerialiser serialiser;
        private MessageAwaiter awaiter;
        public ProtoClient()
        {
            client = new ByteMessageTcpClient();
            client.OnBytesReceived += BytesReceived;
            client.OnDisconnected += Disconnected;
            client.OnConnected += Connected;
            client.MaxIndexedMemory = 1280000000;

            serialiser = new ConcurrentProtoSerialiser();
            awaiter = new MessageAwaiter();
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
            var bytes = serialiser.SerialiseEnvelopedMessage(message);
            client.SendAsync(bytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(MessageEnvelope message, byte[] buffer, int offset, int count)
        {
            byte[] bytes = serialiser.EnvelopeAndSerialiseMessage(message, buffer, offset, count);
            client.SendAsync(bytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage<T>(MessageEnvelope message, T payload) where T : class
        {
            byte[] bytes = serialiser.EnvelopeAndSerialiseMessage(message, payload);
            client.SendAsync(bytes);
        }
        
        public async Task<MessageEnvelope> SendMessageAndWaitResponse(MessageEnvelope message, int timeoutMs = 10000)
        {
            var result = awaiter.RegisterWait(message.MessageId, timeoutMs);

            SendAsyncMessage(message);
            return await result;
        }

        public async Task<MessageEnvelope> SendMessageAndWaitResponse<T>(MessageEnvelope message, T payload, int timeoutMs = 10000) where T : class
        {
            var result = awaiter.RegisterWait(message.MessageId, timeoutMs);

            SendAsyncMessage(message, payload);
            return await result;
        }

        public async Task<MessageEnvelope> SendMessageAndWaitResponse(MessageEnvelope message, byte[] buffer, int offset, int count, int timeoutMs = 10000)
        {
            var result = awaiter.RegisterWait(message.MessageId, timeoutMs);

            SendAsyncMessage(message, buffer, offset, count);
            return await result;
        }

        private void BytesReceived(byte[] bytes, int offset, int count)
        {
            //MessageEnvelope message = serialiser.Deserialize<MessageEnvelope>(bytes, offset, count);
            MessageEnvelope message = serialiser.DeserialiseEnvelopedMessage(bytes, offset, count);

            if(awaiter.IsWaiting(message.MessageId))
                awaiter.ResponseArrived(message);
            else
                OnMessageReceived?.Invoke(message);
           
        }


    }
}
