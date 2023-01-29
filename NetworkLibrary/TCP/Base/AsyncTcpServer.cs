using System;
using NetworkLibrary.TCP.Base;
using NetworkLibrary.Utils;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetworkLibrary.Components.Statistics;

namespace NetworkLibrary.TCP.Base
{
    public class AsyncTcpServer:TcpServerBase
    {
        public ClientAccepted OnClientAccepted;
        public BytesRecieved OnBytesReceived;
        public ClientDisconnected OnClientDisconnected;
        public ClientConnectionRequest OnClientAccepting = (socket) => true;
        public int SessionCount => Sessions.Count;
        public bool Stopping { get; private set; }

        protected Socket ServerSocket;
        protected ConcurrentDictionary<Guid, IAsyncSession> Sessions { get; } = new ConcurrentDictionary<Guid, IAsyncSession>();

        private TcpServerStatisticsPublisher statisticsPublisher;

        public AsyncTcpServer(int port = 20008)
        {
            ServerPort = port;
        }

        #region Start
        public override void StartServer()
        {
            ServerSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            ServerSocket.NoDelay = NaggleNoDelay;
            ServerSocket.ReceiveBufferSize = ServerSockerReceiveBufferSize;
            ServerSocket.Bind(new IPEndPoint(IPAddress.Any, ServerPort));
            ServerSocket.Listen(10000);

            SocketAsyncEventArgs e = new SocketAsyncEventArgs();
            e.Completed += Accepted;
            if (!ServerSocket.AcceptAsync(e))
            {
                Accepted(null, e);
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
                return;
            }

            if (!IsConnectionAllowed(acceptedArg))
            {
                acceptedArg.Dispose();
                return;
            }
            
            Guid clientGuid = Guid.NewGuid();
            var session = CreateSession(acceptedArg, clientGuid);

            session.OnBytesRecieved += HandleBytesRecieved;
            session.OnSessionClosed += HandleDeadSession;

            Sessions.TryAdd(clientGuid, session);

            string msg = "Accepted with port: " + ((IPEndPoint)acceptedArg.AcceptSocket.RemoteEndPoint).Port +
                         " Ip: " + ((IPEndPoint)acceptedArg.AcceptSocket.RemoteEndPoint).Address.ToString();

            MiniLogger.Log(MiniLogger.LogLevel.Info, msg);
            session.StartSession();

            HandleClientAccepted(clientGuid, acceptedArg);
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
        protected virtual IAsyncSession CreateSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            var session = new TcpSession(e, sessionId);
            session.socketSendBufferSize = ClientSendBufsize;
            session.socketRecieveBufferSize = ClientReceiveBufsize;
            session.MaxIndexedMemory = MaxIndexedMemoryPerClient;
            session.DropOnCongestion = DropOnBackPressure;
            session.OnSessionClosed +=(id)=> OnClientDisconnected?.Invoke(id);

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

        public override void SendBytesToClient(in Guid id, byte[] bytes)
        {
            if(Sessions.TryGetValue(id,out var session))
                session.SendAsync(bytes);
        }

        public void SendBytesToClient(in Guid id, byte[] bytes, int offset, int count)
        {
            if (Sessions.TryGetValue(id, out var session))
                session.SendAsync(bytes,offset,count);
        }

        protected virtual void HandleBytesRecieved(Guid guid, byte[] bytes, int offset, int count)
        {
            OnBytesReceived?.Invoke(in guid, bytes, offset, count);
        }

        #endregion


        protected virtual void HandleDeadSession(Guid sessionId)
        {
            Sessions.TryRemove(sessionId, out _);
        }

        protected virtual void HandleError(SocketError error, string context)
        {
            MiniLogger.Log(MiniLogger.LogLevel.Error, context + Enum.GetName(typeof(SocketError), error));
        }

        public override void CloseSession(Guid sessionId)
        {
            if(Sessions.TryGetValue(sessionId,out var session))
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
            if(Sessions.TryGetValue(sessionId, out var session))
            {
                return session.RemoteEndpoint;
            }

            return null;
        }
    }


}
