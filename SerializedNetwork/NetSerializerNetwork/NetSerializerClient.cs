﻿using NetworkLibrary.MessageProtocol;
using NetSerializerNetwork.Components;
using NetSerializerNetwork.Internal;
using NetworkLibrary.Components.Statistics;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace NetSerializerNetwork
{
    public class NetSerializerClient
    {
        public Action<MessageEnvelope> OnMessageReceived;
        public Action OnDisconnected;

        private NetSerializerClientInternal client;
        private GenericMessageSerializer<MessageEnvelope, NetSerialiser> serialiser = new GenericMessageSerializer<MessageEnvelope, NetSerialiser>();

        public NetSerializerClient()
        {
            client = new NetSerializerClientInternal();
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
            return client.SendMessageAndWaitResponse<T>(message, payload, timeoutMs);
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