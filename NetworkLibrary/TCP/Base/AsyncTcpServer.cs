using NetworkLibrary.Components.Statistics;
using NetworkLibrary.Utils;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkLibrary.TCP.Base
{
    public class AsyncTcpServer : TcpServerBase
    {
        /// <summary>
        /// Invoked when client is connected to server.
        /// </summary>
        public ClientAccepted OnClientAccepted;
        /// <summary>
        /// Invoked when bytes are received. New receive operation will not be performed until this callback is finalised.
        /// <br/><br/>Callback data is region of the socket buffer.
        /// <br/>Do a copy if you intend to store the data or use it on different thread.
        /// </summary>
        public BytesRecieved OnBytesReceived;
        /// <summary>
        /// Invoked when client is disconnected
        /// </summary>
        public ClientDisconnected OnClientDisconnected;
        /// <summary>
        /// if returned true client will be accepted by server else it will be dropped
        /// </summary>
        public ClientConnectionRequest OnClientAccepting = (socket) => true;
        public int SessionCount => Sessions.Count;
        public bool Stopping { get; private set; }

        protected Socket ServerSocket;
        internal ConcurrentDictionary<Guid, IAsyncSession> Sessions { get; private set; } = new ConcurrentDictionary<Guid, IAsyncSession>();

        private TcpServerStatisticsPublisher statisticsPublisher;
        private IPEndPoint endpointToBind;
        public IPEndPoint LocalEndpoint =>(IPEndPoint)ServerSocket.LocalEndPoint;
        public AsyncTcpServer(int port)
        {
            ServerPort = port;
            endpointToBind = new IPEndPoint(IPAddress.Any, ServerPort);
        }
        public AsyncTcpServer(IPEndPoint endpointToBind)
        {
            ServerPort = endpointToBind.Port;
            this.endpointToBind = endpointToBind;
        }

        #region Start
        public override void StartServer()
        {
            ServerSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            ServerSocket.NoDelay = NaggleNoDelay;
            ServerSocket.ReceiveBufferSize = ServerSockerReceiveBufferSize;
            ServerSocket.Bind(endpointToBind);
            ServerSocket.Listen(10000);

            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                SocketAsyncEventArgs e = new SocketAsyncEventArgs();
                e.Completed += Accepted;
                if (!ServerSocket.AcceptAsync(e))
                {
                    ThreadPool.UnsafeQueueUserWorkItem((s) => Accepted(null, e), null);
                }
            }

            statisticsPublisher = new TcpServerStatisticsPublisher(Sessions);
        }


        #endregion Start

        #region Accept
        private void Accepted(object sender, SocketAsyncEventArgs acceptedArg)
        {
            if (Stopping)
                return;

            SocketAsyncEventArgs nextClient = new SocketAsyncEventArgs();
            nextClient.Completed += Accepted;

            if (!ServerSocket.AcceptAsync(nextClient))
            {
                ThreadPool.UnsafeQueueUserWorkItem((s) => Accepted(null, nextClient), null);
            }

            if (acceptedArg.SocketError != SocketError.Success)
            {
                HandleError(acceptedArg.SocketError, "While Accepting Client an Error Occured :");
                acceptedArg.Dispose();
                return;
            }

            if (!IsConnectionAllowed(acceptedArg))
            {
                acceptedArg.Dispose();
                return;
            }

            Guid clientGuid = Guid.NewGuid();
            var session = CreateSession(acceptedArg, clientGuid);

            session.OnBytesRecieved += HandleBytesReceived;
            session.OnSessionClosed += HandleDeadSession;

            Sessions.TryAdd(clientGuid, session);

            string msg = "Accepted with port: " + ((IPEndPoint)acceptedArg.AcceptSocket.RemoteEndPoint).Port +
                         " Ip: " + ((IPEndPoint)acceptedArg.AcceptSocket.RemoteEndPoint).Address.ToString();

            MiniLogger.Log(MiniLogger.LogLevel.Info, msg);
            session.StartSession();

            HandleClientAccepted(clientGuid, acceptedArg);

            acceptedArg.Dispose();
        }

        protected virtual bool IsConnectionAllowed(SocketAsyncEventArgs acceptArgs)
        {
            return OnClientAccepting.Invoke(acceptArgs.AcceptSocket);
        }

        protected virtual void HandleClientAccepted(Guid clientGuid, SocketAsyncEventArgs e)
        {
            OnClientAccepted?.Invoke(clientGuid);
        }

        #endregion Accept

        #region Create Session
        private protected virtual IAsyncSession CreateSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            var session = new TcpSession(e, sessionId);
            session.socketSendBufferSize = ClientSendBufsize;
            session.SocketRecieveBufferSize = ClientReceiveBufsize;
            session.MaxIndexedMemory = MaxIndexedMemoryPerClient;
            session.DropOnCongestion = DropOnBackPressure;
            //session.OnSessionClosed += (id) => OnClientDisconnected?.Invoke(id);

            if (GatherConfig == ScatterGatherConfig.UseQueue)
                session.UseQueue = true;
            else
                session.UseQueue = false;

            return session;
        }
        #endregion

        #region Send & Receive
        public override void SendBytesToAllClients(byte[] bytes)
        {
            Parallel.ForEach(Sessions, session =>
            {
                session.Value.SendAsync(bytes);
            });
        }

        /// <summary>
        /// Sends a message to client without blocking.
        /// <br/>If ScatterGatherConfig.UseQueue is selected message will be added to queue without copy.
        /// <br/>If ScatterGatherConfig.UseBuffer message will be copied to message buffer on caller thread.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="bytes"></param>
        public override void SendBytesToClient(Guid id, byte[] bytes)
        {
            if (Sessions.TryGetValue(id, out var session))
                session.SendAsync(bytes);
        }

        /// <summary>
        /// Sends a message without blocking
        /// <br/>If ScatterGatherConfig.UseQueue is selected message will be copied to single buffer before added into queue.
        /// <br/>If ScatterGatherConfig.UseBuffer message will be copied to message buffer on caller thread,
        /// <br/>ScatterGatherConfig.UseBuffer is the reccomeded configuration if your sends are buffer region
        /// </summary>
        /// <param name="id"></param>
        /// <param name="bytes"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public void SendBytesToClient(Guid id, byte[] bytes, int offset, int count)
        {
            if (Sessions.TryGetValue(id, out var session))
                session.SendAsync(bytes, offset, count);
        }

        protected virtual void HandleBytesReceived(Guid guid, byte[] bytes, int offset, int count)
        {
            OnBytesReceived?.Invoke(guid, bytes, offset, count);
        }

        #endregion


        protected virtual void HandleDeadSession(Guid sessionId)
        {
            OnClientDisconnected?.Invoke(sessionId);
            Sessions.TryRemove(sessionId, out _);
        }

        protected virtual void HandleError(SocketError error, string context)
        {
            MiniLogger.Log(MiniLogger.LogLevel.Error, context + Enum.GetName(typeof(SocketError), error));
        }

        public override void CloseSession(Guid sessionId)
        {
            if (Sessions.TryGetValue(sessionId, out var session))
            {
                session.EndSession();
            }
        }
        public override void ShutdownServer()
        {
            Stopping = true;
            try
            {
                ServerSocket.Close();
                ServerSocket.Dispose();
            }
            catch { }


            foreach (var session in Sessions)
            {
                try
                {
                    session.Value.EndSession();
                }
                catch { }
            }
            GC.Collect();

        }

        public override void GetStatistics(out TcpStatistics generalStats, out ConcurrentDictionary<Guid, TcpStatistics> sessionStats)
        {
            statisticsPublisher.GetStatistics(out generalStats, out sessionStats);
        }

        public override IPEndPoint GetSessionEndpoint(Guid sessionId)
        {
            if (Sessions.TryGetValue(sessionId, out var session))
            {
                return session.RemoteEndpoint;
            }

            return null;
        }
    }


}
