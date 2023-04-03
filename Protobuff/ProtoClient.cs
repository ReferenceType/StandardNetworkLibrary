using NetworkLibrary.Components;
using NetworkLibrary.Components.Statistics;
using NetworkLibrary.TCP.Base;
using NetworkLibrary.TCP.ByteMessage;
using NetworkLibrary.TCP.SSL.ByteMessage;
using NetworkLibrary.Utils;
using ProtoBuf;
using Protobuff;
using Protobuff.Components.ProtoTcp;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Protobuff
{
    public class ProtoClient
    {
        public Action<MessageEnvelope> OnMessageReceived;
        public Action OnDisconnected;

        private ProtoClientInternal client;
        private ConcurrentProtoSerialiser serialiser;
        private MessageAwaiter awaiter;
        private SharerdMemoryStreamPool streamPool = new SharerdMemoryStreamPool();

       
        public ProtoClient()
        {
            client = new ProtoClientInternal();
            client.OnBytesReceived += BytesReceived;
            client.OnDisconnected += Disconnected;
            client.MaxIndexedMemory = 128000000;

            client.DeserializeMessages = false;
            client.GatherConfig = ScatterGatherConfig.UseBuffer;

            serialiser = new ConcurrentProtoSerialiser();
            awaiter = new MessageAwaiter();
        }

        public void Connect(string host, int port)
        {
            client.Connect(host, port);
        }
        public Task<bool> ConnectAsync(string host, int port)
        {
            return client.ConnectAsyncAwaitable(host, port);
        }

        public void Disconnect()
        {
            client.Disconnect();
        }

        private void Disconnected()
        {
            OnDisconnected?.Invoke();
        }
        #region Send
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(MessageEnvelope message)
        {
            client.SendAsyncMessage(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(MessageEnvelope message, byte[] buffer, int offset, int count)
        {
            message.SetPayload(buffer, offset, count);
            client.SendAsyncMessage(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage<T>(MessageEnvelope message, T payload) where T : IProtoMessage
        {
            client.SendAsyncMessage(message, payload);
        }

        #endregion Send

        #region SendAndWait
        public Task<MessageEnvelope> SendMessageAndWaitResponse(MessageEnvelope message, int timeoutMs = 10000)
        {
            message.MessageId = Guid.NewGuid();
            var task = awaiter.RegisterWait(message.MessageId, timeoutMs);

            SendAsyncMessage(message);
            return task;
        }

        public Task<MessageEnvelope> SendMessageAndWaitResponse<T>(MessageEnvelope message, T payload, int timeoutMs = 10000) where T : IProtoMessage
        {
            message.MessageId = Guid.NewGuid();
            var task = awaiter.RegisterWait(message.MessageId, timeoutMs);

            SendAsyncMessage(message, payload);
            return task;
        }

        public Task<MessageEnvelope> SendMessageAndWaitResponse(MessageEnvelope message, byte[] buffer, int offset, int count, int timeoutMs = 10000)
        {
            message.MessageId = Guid.NewGuid();
            var task = awaiter.RegisterWait(message.MessageId, timeoutMs);

            SendAsyncMessage(message, buffer, offset, count);
            return task;
        }
        #endregion

        private void BytesReceived(byte[] bytes, int offset, int count)
        {
            MessageEnvelope message = serialiser.DeserialiseEnvelopedMessage(bytes, offset, count);
            
            if (awaiter.IsWaiting(message.MessageId))
            {
                message.LockBytes();
                awaiter.ResponseArrived(message);// maybe consolidate bytes here
            }
            else
                OnMessageReceived?.Invoke(message);

        }
        private PooledMemoryStream RentStream()
        {
            return streamPool.RentStream();
        }
        private void ReturnStream(PooledMemoryStream stream)
        {
            streamPool.ReturnStream(stream);
        }
        public void GetStatistics(out TcpStatistics stats)
        {
            client.GetStatistics(out stats);
        }

    }
}
