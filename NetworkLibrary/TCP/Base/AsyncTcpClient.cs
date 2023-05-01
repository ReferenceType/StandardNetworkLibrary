﻿using NetworkLibrary.Components.Statistics;
using NetworkLibrary.Utils;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetworkLibrary.TCP.Base
{
    public class AsyncTpcClient : TcpClientBase, IDisposable
    {
        protected bool connected = false;
        internal IAsyncSession session;

        private TcpClientStatisticsPublisher statisticsPublisher;
        private Socket clientSocket;
        private TaskCompletionSource<bool> connectedCompletionSource;

        public AsyncTpcClient() { }

        #region Connect

        public override void Connect(string IP, int port)
        {
            _ = ConnectAsyncAwaitable(IP, port).Result;
        }

        public override async Task<bool> ConnectAsyncAwaitable(string IP, int port)
        {
            connectedCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ConnectAsync(IP, port);
            return await connectedCompletionSource.Task.ConfigureAwait(false);
        }

        public override void ConnectAsync(string IP, int port)
        {
            MiniLogger.Log(MiniLogger.LogLevel.Info, "Client Connecting.. ");
            IsConnecting = true;

            clientSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);

            var clientSocketRecieveArgs = new SocketAsyncEventArgs();
            clientSocketRecieveArgs.RemoteEndPoint = new IPEndPoint(IPAddress.Parse(IP), port);

            clientSocketRecieveArgs.Completed += Connected;
            if (!clientSocket.ConnectAsync(clientSocketRecieveArgs))
            {
                Connected(null, clientSocketRecieveArgs);
            }
        }

        #endregion Connect

        #region Connected
        private void Connected(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                HandleError(e, "While Connecting an Error Eccured: ");
                OnConnectFailed?.Invoke(e.ConnectByNameError);
                connectedCompletionSource?.TrySetException(new SocketException((int)e.SocketError));
            }
            else
            {
                e.AcceptSocket = e.ConnectSocket;
                connected = true;

                HandleConnected(e);
                connectedCompletionSource?.TrySetResult(true);
            }
        }

        protected virtual void HandleConnected(SocketAsyncEventArgs e)
        {
            var Id = Guid.NewGuid();
            session = CreateSession(e, Id);

            session.OnBytesRecieved += (sessionId, bytes, offset, count) => HandleBytesRecieved(bytes, offset, count);
            session.OnSessionClosed += (sessionId) => OnDisconnected?.Invoke();

            session.StartSession();
            statisticsPublisher = new TcpClientStatisticsPublisher(session, Id);
            OnConnected?.Invoke();
            MiniLogger.Log(MiniLogger.LogLevel.Info, "Client Connected.");

            IsConnected = true;
        }
        #endregion Connected

        #region Create Session Dependency

        protected virtual IAsyncSession CreateSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            var ses = new TcpSession(e, sessionId);
            ses.socketSendBufferSize = SocketSendBufferSize;
            ses.SocketRecieveBufferSize = SocketRecieveBufferSize;
            ses.MaxIndexedMemory = MaxIndexedMemory;
            ses.DropOnCongestion = DropOnCongestion;
            ses.OnSessionClosed += (id) => OnDisconnected?.Invoke();

            if (GatherConfig == ScatterGatherConfig.UseQueue)
                ses.UseQueue = true;
            else
                ses.UseQueue = false;
            return ses;
        }

        #endregion

        #region Send & Receive
        public override void SendAsync(byte[] buffer)
        {
            if (connected)
                session?.SendAsync(buffer);
        }

        public override void SendAsync(byte[] buffer, int offset, int count)
        {
            if (connected)
                session?.SendAsync(buffer, offset, count);
        }

        protected virtual void HandleBytesRecieved(byte[] bytes, int offset, int count)
        {
            OnBytesReceived?.Invoke(bytes, offset, count);
        }

        #endregion Send & Receive

        private void HandleError(SocketAsyncEventArgs e, string context)
        {
            string msg = "An error Occured While " + context +
                " Error: " + Enum.GetName(typeof(SocketError), e.SocketError);

            MiniLogger.Log(MiniLogger.LogLevel.Error, msg);

        }

        public override void Disconnect()
        {
            // session will fire OnDisconnected event of its own;
            session.EndSession();
            IsConnected = false;
        }

        public void Dispose()
        {
            if (IsConnected)
            {
                try
                {
                    Disconnect();
                    clientSocket?.Dispose();

                }
                catch { }
            }

        }

        public override void GetStatistics(out TcpStatistics generalStats)
        {
            statisticsPublisher.GetStatistics(out generalStats);
        }
    }
}
