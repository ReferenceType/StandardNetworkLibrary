
using NetworkLibrary.Components;
using NetworkLibrary.Components.Statistics;
using NetworkLibrary.TCP.ByteMessage;
using NetworkLibrary.TCP.SSL.ByteMessage;
using NetworkLibrary.Utils;
using ProtoBuf;
using ProtoBuf.Serializers;
using ProtoBuf.WellKnownTypes;
using Protobuff;
using Protobuff.Components.ProtoTcp;
using Protobuff.P2P;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Protobuff
{
    public class ProtoServer
    {
        public delegate void MessageReceived(in Guid clientId, MessageEnvelope message);
        public MessageReceived OnMessageReceived;

        public Action<Guid> OnClientAccepted;
        public Action<Guid> OnClientDisconnected;

        internal readonly ProtoServerInternal server;
        private ConcurrentProtoSerialiser serialiser = new ConcurrentProtoSerialiser();
        private MessageAwaiter awaiter;


        public ProtoServer(int port)
        {
            awaiter = new MessageAwaiter();

            server = new ProtoServerInternal(port);
            server.DeserializeMessages = false;
            server.OnClientAccepted += HandleClientAccepted;
            server.OnBytesReceived += OnBytesReceived;
            server.OnClientDisconnected += HandleClientDisconnected;

            server.MaxIndexedMemoryPerClient = 128000000;
            server.StartServer();

        }
        public void GetStatistics(out TcpStatistics generalStats, out ConcurrentDictionary<Guid, TcpStatistics> sessionStats)
           => server.GetStatistics(out generalStats, out sessionStats);

        public IPEndPoint GetIPEndPoint(Guid cliendId)
            => server.GetSessionEndpoint(cliendId);

        #region Send
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(in Guid clientId, MessageEnvelope message)
        {
            server.SendAsyncMessage(clientId, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(in Guid clientId, MessageEnvelope message, byte[] buffer, int offset, int count)
        {
            message.SetPayload(buffer, offset, count);
            server.SendAsyncMessage(clientId, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage<T>(in Guid clientId, MessageEnvelope message, T payload) where T : IProtoMessage
        {
            server.SendAsyncMessage<T>(clientId, message, payload);
        }
        #endregion

        #region SendAndWait
        public async Task<MessageEnvelope> SendMessageAndWaitResponse<T>(Guid clientId, MessageEnvelope message, byte[] buffer, int offset, int count, int timeoutMs = 10000)
        {
            if (message.MessageId == Guid.Empty)
                message.MessageId = Guid.NewGuid();

            var result = awaiter.RegisterWait(message.MessageId, timeoutMs);
            SendAsyncMessage(clientId, message, buffer, offset, count);
            return await result;
        }

        public async Task<MessageEnvelope> SendMessageAndWaitResponse<T>(Guid clientId, MessageEnvelope message, T payload, int timeoutMs = 10000) where T : IProtoMessage
        {
            if (message.MessageId == Guid.Empty)
                message.MessageId = Guid.NewGuid();

            var result = awaiter.RegisterWait(message.MessageId, timeoutMs);
            SendAsyncMessage(clientId, message, payload);
            return await result;
        }

        public async Task<MessageEnvelope> SendMessageAndWaitResponse(Guid clientId, MessageEnvelope message, int timeoutMs = 10000)
        {
            if (message.MessageId == Guid.Empty)
                message.MessageId = Guid.NewGuid();

            var result = awaiter.RegisterWait(message.MessageId, timeoutMs);
            SendAsyncMessage(clientId, message);
            return await result;
        }
        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual void OnBytesReceived(in Guid guid, byte[] bytes, int offset, int count)
        {
            MessageEnvelope message = serialiser.DeserialiseEnvelopedMessage(bytes, offset, count);
            if (!CheckAwaiter(message))
            {
                OnMessageReceived?.Invoke(in guid, message);
            }
        }

        protected bool CheckAwaiter(MessageEnvelope message)
        {
            if (awaiter.IsWaiting(message.MessageId))
            {
                message.LockBytes();
                awaiter.ResponseArrived(message);
                return true;
            }
            return false;
        }

        protected virtual void HandleClientAccepted(Guid clientId) 
            => OnClientAccepted?.Invoke(clientId);
        protected virtual void HandleClientDisconnected(Guid guid) 
            => OnClientDisconnected?.Invoke(guid);
        public void Shutdown() 
            => server.ShutdownServer();


    }
}

