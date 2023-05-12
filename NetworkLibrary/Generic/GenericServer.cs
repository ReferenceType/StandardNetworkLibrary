using NetworkLibrary.MessageProtocol;
using NetworkLibrary.TCP.Base;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static NetworkLibrary.TCP.Base.TcpServerBase;
using System.Collections.Concurrent;
using NetworkLibrary.Components.Statistics;

namespace NetworkLibrary.Generic
{
    public class GenericServer<S> where S : ISerializer, new()

    {
        GenericServerInternal<S> server;
        public BytesRecieved BytesReceived;
        public ClientAccepted ClientAccepted;
        public ClientDisconnected ClientDisconnected;


        public GenericServer(int port, bool writeLenghtPrefix = true)
        {
            server = new GenericServerInternal<S>(port, writeLenghtPrefix);
            server.GatherConfig = ScatterGatherConfig.UseBuffer;
            server.OnBytesReceived += OnBytesReceived;
            server.OnClientAccepted += OnClientAccepted_;
            server.OnClientDisconnected += OnClientDisconnected_;
        }

        private void OnClientDisconnected_(Guid guid)
        {
            ClientDisconnected?.Invoke(guid);
        }

        private void OnClientAccepted_(Guid guid)
        {
            ClientAccepted?.Invoke(guid);
        }

        private void OnBytesReceived(in Guid guid, byte[] bytes, int offset, int count)
        {
            BytesReceived?.Invoke(guid, bytes,offset,count);
        }

        public void StartServer()=>server.StartServer();
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

    internal class GenericServerInternal<S> : AsyncTcpServer
     where S : ISerializer, new()
    {
        public readonly S serializer =  new S();
        private readonly bool writeLenghtPrefix;
        public GenericServerInternal(int port, bool writeLenghtPrefix = true) : base(port)
        {
            this.writeLenghtPrefix = writeLenghtPrefix;
        }

        public override void StartServer()
        {
            base.StartServer();
        }

        private GenericSession<S> MakeSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            return new GenericSession<S>(e, sessionId, writeLenghtPrefix);
        }

        protected sealed override IAsyncSession CreateSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            var session = MakeSession(e, sessionId);//new GenericMessageSession(e, sessionId);
            session.SocketRecieveBufferSize = ClientReceiveBufsize;
            session.MaxIndexedMemory = MaxIndexedMemoryPerClient;
            session.DropOnCongestion = DropOnBackPressure;
            session.OnSessionClosed += (id) => OnClientDisconnected?.Invoke(id);
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
