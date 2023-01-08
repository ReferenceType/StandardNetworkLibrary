using NetworkLibrary.Components;
using NetworkLibrary.Components.Statistics;
using NetworkLibrary.TCP.SSL.ByteMessage;
using NetworkLibrary.Utils;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Protobuff
{
    public class SecureProtoServer
    {
        public delegate void MessageReceived(in Guid clientId, MessageEnvelope message);
        public MessageReceived OnMessageReceived;

        public Action<Guid> OnClientAccepted;
        public Action<Guid> OnClientDisconnected;

        protected readonly SslByteMessageServer server;
        private ConcurrentProtoSerialiser serialiser = new ConcurrentProtoSerialiser();
        private MessageAwaiter awaiter;
        SharerdMemoryStreamPool streamPool = new SharerdMemoryStreamPool();

        public RemoteCertificateValidationCallback RemoteCertificateValidationCallback=>server.RemoteCertificateValidationCallback;

        public SecureProtoServer(int port,X509Certificate2 cerificate)
        {
            server = new SslByteMessageServer(port,cerificate);
            awaiter = new MessageAwaiter();

            server.OnClientAccepted += HandleClientAccepted;
            server.OnBytesReceived += OnBytesReceived;
            server.OnClientDisconnected += HandleClientDisconnected;
            server.RemoteCertificateValidationCallback += DefaultValidationCallback;

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
        #region Send
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
            
            serialiser.EnvelopeMessageWithBytes(stream, message, buffer,offset,count);

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
        #endregion

        #region SendAndWait
        public async Task<MessageEnvelope> SendMessageAndWaitResponse<T>(Guid clientId, MessageEnvelope message, byte[] buffer, int offset, int count, int timeoutMs = 10000)
        {
            if(message.MessageId == Guid.Empty)
                message.MessageId= Guid.NewGuid();

            var result = awaiter.RegisterWait(message.MessageId, timeoutMs);
            message.MessageId = Guid.NewGuid();

            SendAsyncMessage(clientId, message, buffer, offset, count);
            return await result;
        }

        public async Task<MessageEnvelope> SendMessageAndWaitResponse<T>(Guid clientId, MessageEnvelope message, T payload, int timeoutMs = 10000) where T : IProtoMessage
        {
            if (message.MessageId == Guid.Empty)
                message.MessageId = Guid.NewGuid();

            var result = awaiter.RegisterWait(message.MessageId, timeoutMs);
            message.MessageId = Guid.NewGuid();

            SendAsyncMessage(clientId, message, payload);
            return await result;
        }

        public async Task<MessageEnvelope> SendMessageAndWaitResponse(Guid clientId, MessageEnvelope message, int timeoutMs = 10000)
        {
            if (message.MessageId == Guid.Empty)
                message.MessageId = Guid.NewGuid();

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

        public void Shutdown()=>server.ShutdownServer();
        

    }
}
