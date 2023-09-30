using NetworkLibrary.Components;
using NetworkLibrary.Components.Statistics;
using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace NetworkLibrary.MessageProtocol.Fast
{
    public class GenericSecureMessageClientWrapper<S>where S:ISerializer,new()
    {
        public Action<MessageEnvelope> OnMessageReceived;
        public Action OnDisconnected;

        private SecureMessageClient<S> client;

        // hook to override default cb.
        public RemoteCertificateValidationCallback RemoteCertificateValidationCallback;
        private GenericMessageSerializer<S> serialiser = new GenericMessageSerializer<S>();

        public GenericSecureMessageClientWrapper(X509Certificate2 certificate)
        {
            client = new SecureMessageClient<S>(certificate);
            client.OnMessageReceived = (m)=>OnMessageReceived?.Invoke(m);
            client.OnDisconnected += Disconnected;
            client.MaxIndexedMemory = 128000000;

            client.RemoteCertificateValidationCallback += DefaultValidationCallback;

        }

        

        private bool DefaultValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (RemoteCertificateValidationCallback != null)
                return RemoteCertificateValidationCallback.Invoke(sender, certificate, chain, sslPolicyErrors);
            return true;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(MessageEnvelope message, Action<PooledMemoryStream> serializationCallback)
        {
            client.SendAsyncMessage(message, serializationCallback);
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
