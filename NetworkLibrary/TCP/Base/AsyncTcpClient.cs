using NetworkLibrary.TCP.Base.Interface;
using NetworkLibrary.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkLibrary.TCP.Base
{
    public class AsyncTpcClient
    {
        public Action<byte[], int, int> OnBytesRecieved;
        public Action OnConnected;
        public Action OnConnectFailed;
        public Action OnDisconnected;

        private BufferProvider bufferManager;
        public BufferProvider BufferManager
        {
            get => bufferManager;
            set
            {
                if (IsConnecting)
                    throw new InvalidOperationException("Setting buffer manager is not supported after conection is initiated.");
                bufferManager = value;
            }
        }

        private Socket clientSocket;
        private TaskCompletionSource<bool> connectedCompletionSource;

        protected bool connected = false;
        protected IAsyncSession session;
        public bool IsConnecting { get; private set; }
        public bool IsConnected { get; private set; }

        // Config
        public int SocketSendBufferSize = 128000;
        public int SocketRecieveBufferSize = 128000;
        public int MaxIndexedMemory = 128000;
        public bool DropOnCongestion = false;

        public AsyncTpcClient()
        {

        }

        public async Task<bool> ConnectAsyncAwaitable(string IP, int port)
        {
            connectedCompletionSource = new TaskCompletionSource<bool>();
            ConnectAsync(IP, port);
            return await connectedCompletionSource.Task;
        }

        public void ConnectAsync(string IP, int port)
        {
            MiniLogger.Log(MiniLogger.LogLevel.Info, "Client Connecting.. ");
            IsConnecting = true;

            clientSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);

            clientSocket.ReceiveBufferSize = SocketRecieveBufferSize;
            clientSocket.SendBufferSize = SocketSendBufferSize;

            var clientSocketRecieveArgs = new SocketAsyncEventArgs();
            clientSocketRecieveArgs.RemoteEndPoint = new IPEndPoint(IPAddress.Parse(IP), port);

            clientSocketRecieveArgs.Completed += Connected;
            if (!clientSocket.ConnectAsync(clientSocketRecieveArgs))
            {
                Connected(null, clientSocketRecieveArgs);
            }
        }

        private void Connected(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                HandleError(e, "While Connecting an Error Eccured: ");
                OnConnectFailed?.Invoke();
                connectedCompletionSource?.SetException(new SocketException((int)e.SocketError));
            }
            else
            {
                e.AcceptSocket = e.ConnectSocket;
                connected = true;

                HandleConnected(e);
                connectedCompletionSource?.SetResult(true);

            }
        }

        public virtual void SendAsync(byte[] buffer)
        {
            if (connected)
                session?.SendAsync(buffer);
        }
        public void Disconnect()
        {
            session.EndSession();
        }

        private void HandleError(SocketAsyncEventArgs e, string context)
        {
            string msg = "An error Occured While " + context +
                " associated port: "
                + ((IPEndPoint)e.AcceptSocket.RemoteEndPoint).Port +
                "IP: "
                + ((IPEndPoint)e.AcceptSocket.RemoteEndPoint).Address.ToString() +
                " Error: " + Enum.GetName(typeof(SocketError), e.SocketError);

            MiniLogger.Log(MiniLogger.LogLevel.Error, msg);

        }

        private void ClientDisconnected(object sender, SocketAsyncEventArgs e)
        {
            e.AcceptSocket.Close();
            e.AcceptSocket.Dispose();
            e.Dispose();
            IsConnected = false;
        }

        protected virtual void HandleConnected(SocketAsyncEventArgs e)
        {
            if (bufferManager == null)
            {
                bufferManager = new BufferProvider(1, SocketSendBufferSize, 1, SocketRecieveBufferSize);
            }

            CreateSession(e, Guid.NewGuid(), bufferManager);
            session.OnBytesRecieved += (sessionId, bytes, offset, count) => HandleBytesRecieved(bytes, offset, count);
            session.OnSessionClosed += (sessionId) => OnDisconnected?.Invoke();

            session.StartSession();
            OnConnected?.Invoke();
            MiniLogger.Log(MiniLogger.LogLevel.Info, "Client Connected.");

            IsConnected = true;
        }
        protected virtual void CreateSession(SocketAsyncEventArgs e, Guid sessionId, BufferProvider bufferManager)
        {
            var ses = new TcpSession(e, sessionId, bufferManager);
            ses.socketSendBufferSize = SocketSendBufferSize;
            ses.socketRecieveBufferSize = SocketRecieveBufferSize;
            ses.maxIndexedMemory = MaxIndexedMemory;
            ses.dropOnCongestion = DropOnCongestion;
            session = ses;
        }

        protected virtual void HandleBytesRecieved(byte[] bytes, int offset, int count)
        {
            OnBytesRecieved?.Invoke(bytes, offset, count);
        }

    }
}
