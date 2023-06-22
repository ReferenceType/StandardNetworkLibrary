using NetworkLibrary.Components.Statistics;
using NetworkLibrary;
using NetworkLibrary.MessageProtocol.Serialization;
using Protobuff.Components.Internal;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using NetworkLibrary.Components;

namespace Protobuff
{
    public class SecureProtoMessageServer
    {
        public delegate void MessageReceived(in Guid clientId, MessageEnvelope message);
        public MessageReceived OnMessageReceived;

        public Action<Guid> OnClientAccepted;
        public Action<Guid> OnClientDisconnected;

        internal readonly SecureProtoServerInternal server;
        private ConcurrentProtoSerialiser serialiser = new ConcurrentProtoSerialiser();

        public RemoteCertificateValidationCallback RemoteCertificateValidationCallback;

        public SecureProtoMessageServer(int port, X509Certificate2 cerificate)
        {
            server = new SecureProtoServerInternal(port, cerificate);
            server.DeserializeMessages = false;

            server.OnClientAccepted += HandleClientAccepted;
            server.OnBytesReceived += OnBytesReceived;

            server.OnClientDisconnected += HandleClientDisconnected;
            server.RemoteCertificateValidationCallback += DefaultValidationCallback;

            server.MaxIndexedMemoryPerClient = 228000000;
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
            if (RemoteCertificateValidationCallback != null)
                return RemoteCertificateValidationCallback.Invoke(sender, certificate, chain, sslPolicyErrors);
            return true;
        }

        #region Send

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(in Guid clientId, MessageEnvelope message)
        {
            server.SendAsyncMessage(clientId, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(in Guid clientId, MessageEnvelope message, byte[] buffer, int offset, int count)
        {
            //message.SetPayload(buffer, offset, count);
            server.SendAsyncMessage(clientId, message, buffer, offset, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage<T>(in Guid clientId, MessageEnvelope message, T payload) where T : IProtoMessage
        {
            server.SendAsyncMessage(clientId, message, payload);
        }
        public void SendAsyncMessage(Guid toId, MessageEnvelope envelope, Action<PooledMemoryStream> serializationCallback)
        {
           
            server.SendAsyncMessage(toId,envelope, serializationCallback);
        }
        #endregion

        #region SendAndWait
        public Task<MessageEnvelope> SendMessageAndWaitResponse(Guid clientId, MessageEnvelope message, byte[] buffer, int offset, int count, int timeoutMs = 10000)
        {
            return server.SendMessageAndWaitResponse<MessageEnvelope>(clientId, message, buffer, offset, count);
        }

        public Task<MessageEnvelope> SendMessageAndWaitResponse<T>(Guid clientId, MessageEnvelope message, T payload, int timeoutMs = 10000) where T : IProtoMessage
        {
            return server.SendMessageAndWaitResponse(clientId, message, payload, timeoutMs);
        }

        public Task<MessageEnvelope> SendMessageAndWaitResponse(Guid clientId, MessageEnvelope message, int timeoutMs = 10000)
        {
            return server.SendMessageAndWaitResponse(clientId, message, timeoutMs);
        }
        #endregion

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
            if (server.awaiter.IsWaiting(message.MessageId))
            {
                message.LockBytes();
                server.awaiter.ResponseArrived(message);
                return true;
            }
            return false;
        }

        public void Shutdown() => server.ShutdownServer();


    }


}
