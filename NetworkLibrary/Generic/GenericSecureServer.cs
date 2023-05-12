using NetworkLibrary.MessageProtocol;
using NetworkLibrary.TCP.Base;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using NetworkLibrary.TCP.SSL.Base;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using static NetworkLibrary.TCP.Base.TcpServerBase;
using System.Collections.Concurrent;
using NetworkLibrary.Components.Statistics;

namespace NetworkLibrary.Generic
{
    public class GenericSecureServer<S> where S : ISerializer, new()

    {
        GenericSecureServerInternal<S> server;
        public BytesRecieved BytesReceived;
        public ClientAccepted ClientAccepted;
        public ClientDisconnected ClientDisconnected;
        public RemoteCertificateValidationCallback CertificateValidationCallback;

        public GenericSecureServer(int port, X509Certificate2 certificate, bool writeLenghtPrefix = true)
        {
            server = new GenericSecureServerInternal<S>(port, certificate, writeLenghtPrefix);
            server.GatherConfig = ScatterGatherConfig.UseBuffer;
            server.OnBytesReceived += OnBytesReceived;
            server.OnClientAccepted += OnClientAccepted;
            server.OnClientDisconnected += OnClientDisconnected;
            server.RemoteCertificateValidationCallback += OnValidationCallback;
        }

        private bool OnValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (CertificateValidationCallback == null)
                return true;
            return CertificateValidationCallback.Invoke(sender, certificate, chain, sslPolicyErrors);
        }

        private void OnClientDisconnected(Guid guid)
        {
            ClientDisconnected?.Invoke(guid);
        }

        private void OnClientAccepted(Guid guid)
        {
            ClientAccepted?.Invoke(guid);
        }

        private void OnBytesReceived(in Guid guid, byte[] bytes, int offset, int count)
        {
            BytesReceived?.Invoke(guid, bytes, offset, count);
        }

        public void StartServer() => server.StartServer();
        public void SendAsync<T>(in Guid clientId, T instance)
        {
            server.SendAsync(in clientId, instance);
        }

        public void Shutdown()
            => server.ShutdownServer();
        public void GetStatistics(out TcpStatistics generalStats, out ConcurrentDictionary<Guid, TcpStatistics> sessionStats)
            => server.GetStatistics(out generalStats, out sessionStats);

        public IPEndPoint GetIPEndPoint(Guid cliendId)
            => server.GetSessionEndpoint(cliendId);
    }

    internal class GenericSecureServerInternal<S> : SslServer
    where S : ISerializer, new()
    {
        public readonly S serializer = new S();
        private readonly bool writeLenghtPrefix;

        public GenericSecureServerInternal(int port, X509Certificate2 certificate, bool writeLenghtPrefix = true) : base(port, certificate)
        {
            this.writeLenghtPrefix = writeLenghtPrefix;
        }


        public override void StartServer()
        {
            base.StartServer();
        }

        private GenericSecureSession<S> MakeSession(Guid sessionId, SslStream sslStream)
        {
            return new GenericSecureSession<S>( sessionId, sslStream, writeLenghtPrefix);
        }
        protected sealed override IAsyncSession CreateSession(Guid guid, ValueTuple<SslStream, IPEndPoint> tuple)
        {
            var session = MakeSession(guid, tuple.Item1);//new SecureProtoSessionInternal(guid, tuple.Item1);
            session.MaxIndexedMemory = MaxIndexedMemoryPerClient;
            session.RemoteEndpoint = tuple.Item2;
            return session;
        }
       

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsync<T>(in Guid clientId, T message)
        {
            if (Sessions.TryGetValue(clientId, out var session))
                ((GenericSession<S>)session).SendAsync(message);

        }

        public IPEndPoint GetIPEndPoint(Guid cliendId)
        {
            return GetSessionEndpoint(cliendId);
        }

    }
}
