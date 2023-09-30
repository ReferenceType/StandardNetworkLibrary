using NetworkLibrary.Components.Statistics;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace NetworkLibrary.MessageProtocol.Fast
{
    public class GenericMessageClientWrapper<S>
       where S : ISerializer, new()
    {
        private MessageClient<S> client;
        public Action<MessageEnvelope> OnMessageReceived;
        public Action OnDisconnected;

        private GenericMessageSerializer<S> serialiser = new GenericMessageSerializer<S>();

        public GenericMessageClientWrapper()
        {
            client = new MessageClient<S>();
            client.OnMessageReceived += HandleMessageReceived;
            client.OnDisconnected += Disconnected;
            client.MaxIndexedMemory = 128000000;

            client.GatherConfig = ScatterGatherConfig.UseBuffer;

        }

        private void HandleMessageReceived(MessageEnvelope message)
        {
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
        public void SendAsyncMessage<T>(MessageEnvelope message, T payload)
        {
            client.SendAsyncMessage(message, payload);
        }

        #endregion Send

        #region SendAndWait
        public Task<MessageEnvelope> SendMessageAndWaitResponse(MessageEnvelope message, int timeoutMs = 10000)
        {
            return client.SendMessageAndWaitResponse(message, timeoutMs);
        }

        public Task<MessageEnvelope> SendMessageAndWaitResponse<T>(MessageEnvelope message, T payload, int timeoutMs = 10000)
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
