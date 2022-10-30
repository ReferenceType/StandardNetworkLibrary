using NetworkLibrary.TCP.Base;
using NetworkLibrary.TCP.ByteMessage;
using NetworkLibrary.TCP.SSL.ByteMessage;
using ProtoBuf;
using Protobuff;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Protobuff
{
    public class SecureProtoClient
    {
        private SslByteMessageClient client;

        public Action<MessageEnvelope> OnMessageReceived;
        //public Action<MessageEnvelope> OnRequestReceived;
        //public Action<MessageEnvelope> OnResponseReceived;

        public Action OnDisconnected;

        private ConcurrentProtoSerialiser serialiser;
        private MessageAwaiter awaiter;
        private ConcurrentBag<MemoryStream> streamPool = new ConcurrentBag<MemoryStream>();

        public RemoteCertificateValidationCallback RemoteCertificateValidationCallback => client.RemoteCertificateValidationCallback;

        public SecureProtoClient(X509Certificate2 certificate)
        {
            streamPool.Add(new MemoryStream());

            client = new SslByteMessageClient(certificate);
            client.OnBytesReceived += BytesReceived;
            client.OnDisconnected += Disconnected;
            client.OnConnected += Connected;
            client.MaxIndexedMemory = 1280000000;

            client.RemoteCertificateValidationCallback += DefaultValidayionCallback;
            client.GatherConfig = ScatterGatherConfig.UseBuffer;

            serialiser = new ConcurrentProtoSerialiser();
            awaiter = new MessageAwaiter();
        }

        private bool DefaultValidayionCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
        private MemoryStream RentStream()
        {
            if (!streamPool.TryTake(out var stream))
            {
                stream = new MemoryStream();
            }
            return stream;
        }
        private void ReturnStream(MemoryStream stream)
        {
            stream.Position = 0;
            streamPool.Add(stream);
        }
        public void Connect(string host, int port)
        {
            client.Connect(host, port);
        }

        private void Connected()
        {
        }
        public void Disconnect()
        {
            client.Disconnect();
        }

        private void Disconnected()
        {
            OnDisconnected?.Invoke();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]

        public void SendAsyncMessage(MessageEnvelope message)
        {
            var stream = RentStream();
            if (message.Payload != null)
                serialiser.EnvelopeMessageWithBytes(stream, message, message.Payload, 0, message.Payload.Length);
            else
                serialiser.EnvelopeMessageWithBytes(stream, message, null, 0, 0);

            client.SendAsync(stream.GetBuffer(), 0, (int)stream.Position);

            ReturnStream(stream);
            //var bytes = serialiser.SerialiseEnvelopedMessage(message);
            //client.SendAsync(bytes);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]

        public void SendAsyncMessage(MessageEnvelope message, byte[] buffer, int offset, int count)
        {
            var stream = RentStream();
            serialiser.EnvelopeMessageWithBytes(stream, message,buffer, offset, count);
            client.SendAsync(stream.GetBuffer(), 0, (int)stream.Position);
            ReturnStream(stream);
            //byte[] bytes = serialiser.EnvelopeAndSerialiseMessage(message, buffer, offset, count);
            //client.SendAsync(bytes);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]

        public void SendAsyncMessage<T>(MessageEnvelope message, T payload) where T : class
        {
            var stream = RentStream();
            serialiser.EnvelopeMessageWithInnerMessage(stream, message, payload);
            client.SendAsync(stream.GetBuffer(), 0, (int)stream.Position);
            ReturnStream(stream);
            //byte[] bytes = serialiser.EnvelopeAndSerialiseMessage(message, payload);
            //client.SendAsync(bytes);
        }

       

        public async Task<MessageEnvelope> SendMessageAndWaitResponse(MessageEnvelope message, int timeoutMs = 10000)
        {
            message.MessageId = Guid.NewGuid();

            var result = awaiter.RegisterWait(message.MessageId, timeoutMs);

            SendAsyncMessage(message);
            return await result;
        }

        public async Task<MessageEnvelope> SendMessageAndWaitResponse<T>(MessageEnvelope message,T payload, int timeoutMs = 10000) where T : class
        {
            message.MessageId = Guid.NewGuid();

            var result = awaiter.RegisterWait(message.MessageId, timeoutMs);

            SendAsyncMessage(message,payload);
            return await result;
        }

        public async Task<MessageEnvelope> SendMessageAndWaitResponse(MessageEnvelope message, byte[] buffer, int offset, int count, int timeoutMs = 10000)
        {
            message.MessageId = Guid.NewGuid();

            var result = awaiter.RegisterWait(message.MessageId, timeoutMs);

            SendAsyncMessage(message, buffer,offset,count);
            return await result;
        }

        private void BytesReceived(byte[] bytes, int offset, int count)
        {
            //MessageEnvelope message = serialiser.Deserialize<MessageEnvelope>(bytes, offset, count);
            MessageEnvelope message = serialiser.DeserialiseEnvelopedMessage(bytes, offset, count);


            if (awaiter.IsWaiting(message.MessageId))
                awaiter.ResponseArrived(message);
            else
                OnMessageReceived?.Invoke(message);

        }


    }
}
