using NetworkLibrary.Components.Statistics;
using NetworkLibrary.MessageProtocol;
using NetworkLibrary.TCP.Base;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static NetworkLibrary.TCP.Base.TcpClientBase;

namespace NetworkLibrary.Generic
{
    public class GenericClient<S> where S : ISerializer, new()
    {
        public Action OnDisconnected;
        public BytesRecieved BytesReceived;
        private GenericClientInternal<S> client;

        public GenericClient(bool writeLenghtPrefix = true)
        {
            client = new GenericClientInternal<S>(writeLenghtPrefix);
            client.OnBytesReceived+=OnBytesReceived;
            client.OnDisconnected += Disconnected;
            client.MaxIndexedMemory = 128000000;
            client.GatherConfig = ScatterGatherConfig.UseBuffer;
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

    internal class GenericClientInternal<S> : AsyncTpcClient
  where S : ISerializer, new()
    {
        public readonly GenericMessageSerializer<S> Serializer =  new GenericMessageSerializer<S>();
        private new GenericSession<S> session;
        private readonly bool writeLenghtPrefix;
        public GenericClientInternal(bool writeLenghtPrefix = true)
        {
            this.writeLenghtPrefix = writeLenghtPrefix;
        }

        protected virtual GenericSession<S> MakeSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            return new GenericSession<S>(e, sessionId, writeLenghtPrefix);
        }

        protected override IAsyncSession CreateSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            var ses = MakeSession(e, sessionId);
            ses.SocketRecieveBufferSize = SocketRecieveBufferSize;
            ses.MaxIndexedMemory = MaxIndexedMemory;
            ses.DropOnCongestion = DropOnCongestion;
            ses.OnSessionClosed += (id) => OnDisconnected?.Invoke();
            session = ses;

            return ses;
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
