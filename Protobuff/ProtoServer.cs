
using NetworkLibrary.Components;
using NetworkLibrary.Components.Statistics;
using NetworkLibrary.TCP.ByteMessage;
using NetworkLibrary.TCP.SSL.ByteMessage;
using NetworkLibrary.Utils;
using ProtoBuf;
using ProtoBuf.Serializers;
using ProtoBuf.WellKnownTypes;
using Protobuff;
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

        protected readonly ByteMessageTcpServer server;
        private ConcurrentProtoSerialiser serialiser = new ConcurrentProtoSerialiser();
        private MessageAwaiter awaiter;
        private SharerdMemoryStreamPool streamPool = new SharerdMemoryStreamPool();


        public ProtoServer(int port)
        {
            server = new ByteMessageTcpServer(port);
            awaiter = new MessageAwaiter();

            server.OnClientAccepted += HandleClientAccepted;
            server.OnBytesReceived += OnBytesReceived;
            server.OnClientDisconnected += HandleClientDisconnected;

            server.GatherConfig = ScatterGatherConfig.UseBuffer;

            server.MaxIndexedMemoryPerClient = 1280000000;
            server.StartServer();

        }

        public void GetStatistics(out TcpStatistics generalStats, out ConcurrentDictionary<Guid, TcpStatistics> sessionStats)
        {
            server.GetStatistics(out generalStats, out sessionStats);
        }
        public IPEndPoint GetIPEndPoint(Guid cliendId)
        {
            return server.GetSessionEndpoint(cliendId);
        }
        private bool DefaultValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        private PooledMemoryStream RentStream()
        {
            return streamPool.RentStream();
        }
        private void ReturnStream(PooledMemoryStream stream)
        {
            stream.Flush();
            streamPool.ReturnStream(stream);
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


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual void OnBytesReceived(in Guid guid, byte[] bytes, int offset, int count)
        {
            MessageEnvelope message = serialiser.DeserialiseEnvelopedMessage(bytes, offset, count);
            if (!CheckAwaiter(message))
            {
                OnMessageReceived?.Invoke(in guid, message);
            }
        }

        protected virtual void HandleClientAccepted(Guid clientId)
        {
            OnClientAccepted?.Invoke(clientId);
        }

        protected virtual void HandleClientDisconnected(Guid guid)
        {
            OnClientDisconnected?.Invoke(guid);
        }

        protected bool CheckAwaiter(MessageEnvelope message)
        {
            if (awaiter.IsWaiting(message.MessageId))
            {
                awaiter.ResponseArrived(message);
                return true;
            }
            return false;
        }

        public void Shutdown() => server.ShutdownServer();


    }
}

