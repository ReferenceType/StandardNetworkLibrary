using NetworkLibrary.Components.Statistics;
using NetworkLibrary;
using NetworkLibrary.MessageProtocol.Serialization;
using Protobuff.Components.Internal;
using System;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using NetworkLibrary.Components;

namespace Protobuff
{
    public class SecureProtoMessageClient
    {
        public Action<MessageEnvelope> OnMessageReceived;
        public Action OnDisconnected;

        private SecureProtoClientInternal client;

        // hook to override default cb.
        public RemoteCertificateValidationCallback RemoteCertificateValidationCallback;
        private ConcurrentProtoSerialiser serialiser = new ConcurrentProtoSerialiser();

        public SecureProtoMessageClient(X509Certificate2 certificate)
        {
            client = new SecureProtoClientInternal(certificate);
            client.DeserializeMessages = false;
            client.OnBytesReceived += BytesReceived;
            //client.OnMessageReceived += HandleMessageReceived;
            client.OnDisconnected += Disconnected;
            client.MaxIndexedMemory = 128000000;

            client.RemoteCertificateValidationCallback += DefaultValidationCallback;

        }

        private void BytesReceived(byte[] bytes, int offset, int count)
        {
            MessageEnvelope message = serialiser.DeserialiseEnvelopedMessage(bytes, offset, count);

            if (client.Awaiter.IsWaiting(message.MessageId))
            {
                message.LockBytes();
                client.Awaiter.ResponseArrived(message);
            }
            else
                OnMessageReceived?.Invoke(message);
        }

        private void HandleMessageReceived(MessageEnvelope obj)
        {
            OnMessageReceived?.Invoke(obj);
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
        public void SendAsyncMessage<T>(MessageEnvelope message, T payload) where T : IProtoMessage
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
