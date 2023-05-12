using MessageProtocol;
using NetworkLibrary.Components.Statistics;
using NetworkLibrary;
using NetworkLibrary.MessageProtocol.Serialization;
using Protobuff.Components.Internal;
using Protobuff.Components.Serialiser;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NetworkLibrary.MessageProtocol;

namespace Protobuff
{
    public class ProtoMessageClient
    {
        public Action<MessageEnvelope> OnMessageReceived;
        public Action OnDisconnected;

        private ProtoClientInternal client;
        private GenericMessageSerializer<ProtoSerializer> serialiser = new GenericMessageSerializer<ProtoSerializer>();

        public ProtoMessageClient()
        {
            client = new ProtoClientInternal();
            client.OnBytesReceived += BytesReceived;
            client.DeserializeMessages = false;
            //client.OnMessageReceived += HandleMessageReceived;
            client.OnDisconnected += Disconnected;
            client.MaxIndexedMemory = 128000000;

            client.GatherConfig = ScatterGatherConfig.UseBuffer;

        }

        private void HandleMessageReceived(MessageEnvelope message)
        {
            OnMessageReceived?.Invoke(message);
        }
        private void BytesReceived(byte[] bytes, int offset, int count)
        {
            MessageEnvelope message = serialiser.DeserialiseEnvelopedMessage(bytes, offset, count);

            if (client.Awaiter.IsWaiting(message.MessageId))
            {
                message.LockBytes();
                client.Awaiter.ResponseArrived(message);// maybe consolidate bytes here
            }
            else
                OnMessageReceived?.Invoke(message);

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
            client.SendAsyncMessage(message, buffer, offset, count);
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
            return client.SendMessageAndWaitResponse(message, timeoutMs);
        }

        public Task<MessageEnvelope> SendMessageAndWaitResponse<T>(MessageEnvelope message, T payload, int timeoutMs = 10000) where T : IProtoMessage
        {
            return client.SendMessageAndWaitResponse(message, payload, timeoutMs);
        }

        public Task<MessageEnvelope> SendMessageAndWaitResponse(MessageEnvelope message, byte[] buffer, int offset, int count, int timeoutMs = 10000)
        {
            return client.SendMessageAndWaitResponse(message, buffer, offset, count, timeoutMs);
        }
        #endregion



        public void GetStatistics(out TcpStatistics stats)
        {
            client.GetStatistics(out stats);
        }

    }
}
