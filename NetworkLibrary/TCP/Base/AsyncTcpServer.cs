using System;
using NetworkLibrary.TCP.Base;
using NetworkLibrary.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace NetworkLibrary.TCP.Base
{
    public class AsyncTcpServer:TcpServerBase
    {
        
        #region Fields & Props

        public ClientAccepted OnClientAccepted;
        public BytesRecieved OnBytesReceived;
        public ClientConnectionRequest OnClientAccepting = (socket) => true;

        public BufferProvider BufferManager { get; private set; }
        protected Socket ServerSocket;
      
        internal ConcurrentDictionary<Guid, IAsyncSession> Sessions { get; } = new ConcurrentDictionary<Guid, IAsyncSession>();

        public int SessionCount=>Sessions.Count;
        public bool Stopping { get; private set; }

        #endregion

        public AsyncTcpServer(int port = 20008, int maxClients = 100)
        {
            ServerPort = port;
            MaxClients = maxClients;
        }

        #region Start
        public override void StartServer()
        {
            BufferManager = new BufferProvider(MaxClients, ClientSendBufsize, MaxClients, ClientReceiveBufsize);

            ServerSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            ServerSocket.NoDelay = NaggleNoDelay;
            ServerSocket.ReceiveBufferSize = ServerSockerReceiveBufferSize;
            ServerSocket.Bind(new IPEndPoint(IPAddress.Any, ServerPort));
            ServerSocket.Listen(MaxClients);


            SocketAsyncEventArgs e = new SocketAsyncEventArgs();
            e.Completed += Accepted;
            if (!ServerSocket.AcceptAsync(e))
            {
                Accepted(null, e);
            }
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

            if (BufferManager.IsExhausted())
                return;

            if (!HandleConnectionRequest(acceptedArg))
            {
                return;
            }
            
            Guid clientGuid = Guid.NewGuid();
            var session = CreateSession(acceptedArg, clientGuid, BufferManager);

            session.OnBytesRecieved += (sessionId, bytes, offset, count) => { HandleBytesRecieved(sessionId, bytes, offset, count); };
            session.OnSessionClosed += (sessionId) => { HandleDeadSession(sessionId); };

            session.StartSession();
            Sessions.TryAdd(clientGuid, session);

            string msg = "Accepted with port: " + ((IPEndPoint)acceptedArg.AcceptSocket.RemoteEndPoint).Port +
                " Ip: " + ((IPEndPoint)acceptedArg.AcceptSocket.RemoteEndPoint).Address.ToString();

            MiniLogger.Log(MiniLogger.LogLevel.Info, msg);

            HandleClientAccepted(clientGuid, acceptedArg);
        }

        protected virtual bool HandleConnectionRequest(SocketAsyncEventArgs acceptArgs)
        {
            return OnClientAccepting.Invoke(acceptArgs.AcceptSocket);
        }

        protected virtual void HandleClientAccepted(Guid clientGuid, SocketAsyncEventArgs e)
        {
            OnClientAccepted?.Invoke(clientGuid);
        }

        #endregion Accept

        #region Create Session
        internal virtual IAsyncSession CreateSession(SocketAsyncEventArgs e, Guid sessionId, BufferProvider bufferManager)
        {
            var session = new TcpSession(e, sessionId, bufferManager);
            session.socketSendBufferSize = ClientSendBufsize;
            session.socketRecieveBufferSize = ClientReceiveBufsize;
            session.maxIndexedMemory = MaxIndexedMemoryPerClient;
            session.dropOnCongestion = DropOnBackPressure;
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

        public override void SendBytesToClient(Guid id, byte[] bytes)
        {
            Sessions[id].SendAsync(bytes);
        }


        protected virtual void HandleBytesRecieved(Guid guid, byte[] bytes, int offset, int count)
        {
            OnBytesReceived?.Invoke(guid, bytes, offset, count);
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
            ServerSocket.Close();
            ServerSocket.Dispose();

            foreach (var session in Sessions)
            {
                try
                {
                    session.Value.EndSession();
                }
                catch { }
            }

            BufferManager=null;
            GC.Collect();

        }


       
    }


}
