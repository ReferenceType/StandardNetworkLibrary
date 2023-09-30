using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Security;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using NetworkLibrary.Components.Statistics;
using NetworkLibrary.Components;

namespace NetworkLibrary.MessageProtocol.Fast
{
    public class GenericSecureMessageServerWrapper<S> where S:ISerializer, new()
    {
        public delegate void MessageReceived(Guid clientId, MessageEnvelope message);
        public MessageReceived OnMessageReceived;

        public Action<Guid> OnClientAccepted;
        public Action<Guid> OnClientDisconnected;

        internal readonly SecureMessageServer<S> server;
        private GenericMessageSerializer<S> serialiser = new GenericMessageSerializer<S>();

        public RemoteCertificateValidationCallback RemoteCertificateValidationCallback;

        public GenericSecureMessageServerWrapper(int port, X509Certificate2 cerificate)
        {
            server = new SecureMessageServer<S>(port, cerificate);

            server.OnClientAccepted += HandleClientAccepted;
            server.OnClientDisconnected += HandleClientDisconnected;
            server.RemoteCertificateValidationCallback += DefaultValidationCallback;
            server.OnMessageReceived = (guid, message) => OnMessageReceived?.Invoke(guid, message);

            server.MaxIndexedMemoryPerClient = 228000000;
           
        }
        public void StartServer()
        {
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
        public void SendAsyncMessage(Guid clientId, MessageEnvelope message)
        {
            server.SendAsyncMessage(clientId, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(Guid clientId, MessageEnvelope message, byte[] buffer, int offset, int count)
        {
            //message.SetPayload(buffer, offset, count);
            server.SendAsyncMessage(clientId, message, buffer, offset, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage<T>(Guid clientId, MessageEnvelope message, T payload) 
        {
            server.SendAsyncMessage(clientId, message, payload);
        }
        public void SendAsyncMessage(Guid toId, MessageEnvelope envelope, Action<PooledMemoryStream> serializationCallback)
        {

            server.SendAsyncMessage(toId, envelope, serializationCallback);
        }
        #endregion

        #region SendAndWait
        public Task<MessageEnvelope> SendMessageAndWaitResponse(Guid clientId, MessageEnvelope message, byte[] buffer, int offset, int count, int timeoutMs = 10000)
        {
            return server.SendMessageAndWaitResponse<MessageEnvelope>(clientId, message, buffer, offset, count);
        }

        public Task<MessageEnvelope> SendMessageAndWaitResponse<T>(Guid clientId, MessageEnvelope message, T payload, int timeoutMs = 10000)
        {
            return server.SendMessageAndWaitResponse(clientId, message, payload, timeoutMs);
        }

        public Task<MessageEnvelope> SendMessageAndWaitResponse(Guid clientId, MessageEnvelope message, int timeoutMs = 10000)
        {
            return server.SendMessageAndWaitResponse(clientId, message, timeoutMs);
        }
        #endregion

       

        protected virtual void HandleClientAccepted(Guid clientId)
        {
            OnClientAccepted?.Invoke(clientId);
        }

        protected virtual void HandleClientDisconnected(Guid guid)
        {
            OnClientDisconnected?.Invoke(guid);
        }


        public void Shutdown() => server.ShutdownServer();
    }
}
