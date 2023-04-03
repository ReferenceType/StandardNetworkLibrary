using NetworkLibrary.Components;
using NetworkLibrary.Components.Statistics;

using NetworkLibrary.Utils;
using ProtoBuf;
using Protobuff;
using Protobuff.Components.TransportWrapper.SecureProtoTcp;
using System;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Protobuff
{
    public class SecureProtoClient
    {
        public Action<MessageEnvelope> OnMessageReceived;
        public Action OnDisconnected;

        private SecureProtoClientInternal client;
        private ConcurrentProtoSerialiser serialiser;
        private MessageAwaiter awaiter;

        // hook to override default cb.
        public RemoteCertificateValidationCallback RemoteCertificateValidationCallback;

        public SecureProtoClient(X509Certificate2 certificate)
        {
            client = new SecureProtoClientInternal(certificate);
            client.DeserializeMessages = false;
            client.OnBytesReceived += BytesReceived;
            client.OnDisconnected += Disconnected;
            client.MaxIndexedMemory = 128000000;

            client.RemoteCertificateValidationCallback += DefaultValidationCallback;

            serialiser = new ConcurrentProtoSerialiser();
            awaiter = new MessageAwaiter();
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
            message.SetPayload(buffer, offset, count);
            client.SendAsyncMessage(message);
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
            message.MessageId = Guid.NewGuid();
            var task = awaiter.RegisterWait(message.MessageId, timeoutMs);

            SendAsyncMessage(message);
            return task;
        }

        public Task<MessageEnvelope> SendMessageAndWaitResponse<T>(MessageEnvelope message,T payload, int timeoutMs = 10000) where T : IProtoMessage
        {
            message.MessageId = Guid.NewGuid();
            var task = awaiter.RegisterWait(message.MessageId, timeoutMs);

            SendAsyncMessage(message,payload);
            return task;
        }

        public Task<MessageEnvelope> SendMessageAndWaitResponse(MessageEnvelope message, byte[] buffer, int offset, int count, int timeoutMs = 10000)
        {
            message.MessageId = Guid.NewGuid();
            var task = awaiter.RegisterWait(message.MessageId, timeoutMs);

            SendAsyncMessage(message, buffer,offset,count);
            return task;
        }
        #endregion

        private void BytesReceived(byte[] bytes, int offset, int count)
        {
            try
            {
                MessageEnvelope message = serialiser.DeserialiseEnvelopedMessage(bytes, offset, count);
                if (awaiter.IsWaiting(message.MessageId))
                {
                    message.LockBytes();
                    awaiter.ResponseArrived(message);
                }
                else
                    OnMessageReceived?.Invoke(message);
            }
            catch (Exception ex)
            {
                MiniLogger.Log(MiniLogger.LogLevel.Error, "Error occured while deseriallizing : "+ex.Message);
            }
        }
      
        public void GetStatistics(out TcpStatistics stats)
        {
             client.GetStatistics(out stats);
        }

    }
}
