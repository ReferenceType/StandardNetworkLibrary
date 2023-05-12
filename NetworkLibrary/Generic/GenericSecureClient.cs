using MessageProtocol;
using NetworkLibrary.Components.Statistics;
using NetworkLibrary.MessageProtocol;
using NetworkLibrary.TCP.Base;
using NetworkLibrary.TCP.SSL.Base;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using static NetworkLibrary.TCP.Base.TcpClientBase;

namespace NetworkLibrary.Generic
{

    public class GenericSecureClient<S> where S : ISerializer, new()
    {
        public Action OnDisconnected;
        public BytesRecieved BytesReceived;
        private GenericSecureClientInternal<S> client;
        public RemoteCertificateValidationCallback CertificateValidationCallback;
        public GenericSecureClient(X509Certificate2 certificate, bool writeLenghtPrefix = true)
        {
            client = new GenericSecureClientInternal<S>(certificate, writeLenghtPrefix);
            client.OnBytesReceived += OnBytesReceived;
            client.OnDisconnected += Disconnected;
            client.MaxIndexedMemory = 128000000;
            client.GatherConfig = ScatterGatherConfig.UseBuffer;
            client.RemoteCertificateValidationCallback += OnValidationCallback;
        }

        private bool OnValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (CertificateValidationCallback == null)
                return true;
            return CertificateValidationCallback.Invoke(sender, certificate,chain,sslPolicyErrors);
        }

        private void OnBytesReceived(byte[] bytes, int offset, int count)
        {
            BytesReceived?.Invoke(bytes, offset, count);
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

        public void SendAsync<T>(T instance)
        {
            client.SendAsync(instance);
        }

        public void GetStatistics(out TcpStatistics stats) => client.GetStatistics(out stats);
    }




    internal class GenericSecureClientInternal<S> : SslClient
   where S : ISerializer, new()
    {
        public readonly GenericMessageSerializer<S> Serializer = new GenericMessageSerializer<S>();
        private new GenericSession<S> session;
        private readonly bool writeLenghtPrefix;

        public GenericSecureClientInternal(X509Certificate2 certificate, bool writeLenghtPrefix = true) : base(certificate)
        {
            this.writeLenghtPrefix = writeLenghtPrefix;
        }
  

        private GenericSecureSession<S> MakeSession(Guid guid, SslStream sslStream)
        {
            return new GenericSecureSession<S>(guid, sslStream, writeLenghtPrefix);
        }
        protected sealed override IAsyncSession CreateSession(Guid guid, ValueTuple<SslStream, IPEndPoint> tuple)
        {
            var session = MakeSession(guid, tuple.Item1);//new SecureProtoSessionInternal(guid, tuple.Item1);
            session.MaxIndexedMemory = MaxIndexedMemory;
            session.RemoteEndpoint = tuple.Item2;           
            return session;
        }
       

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsync<T>(T message)
        {
            session?.SendAsync(message);
        }

        public new Task<bool> ConnectAsync(string host, int port)
        {
            return ConnectAsyncAwaitable(host, port);
        }
    }
}
