using System;
using NetworkLibrary.TCP.Base.Interface;
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
    public class AsyncTcpServer
    {
        public delegate void ClientAccepted(Guid guid, Socket ClientSocket);
        public delegate void BytesRecieved(Guid guid, byte[] bytes, int offset, int count);
        //public delegate bool ClientConnectionRequest(Socket acceptedSocket);
        public ClientAccepted OnClientAccepted;
        public BytesRecieved OnBytesRecieved;
       // public ClientConnectionRequest OnClientAccepting;

        public BufferProvider BufferManager { get; private set; }

        public int MaxClients { get; private set; } = 10000;
        public int ClientSendBufsize = 128000;
        public int ClientReceiveBufsize = 128000;
        public int MaxIndexedMemoryPerClient = 1280000;
        public int SoocketReceiveBufferSize = 2080000000;
        public bool DropOnBackPressure = false;
        public bool NaggleNoDelay = false;

        protected Socket ServerSocket;
        protected int ServerPort { get; }
        public ConcurrentDictionary<Guid, IAsyncSession> Sessions { get; } = new ConcurrentDictionary<Guid, IAsyncSession>();
        public bool Stopping { get; private set; }

        public AsyncTcpServer(int port = 20008, int maxClients = 100)
        {
            ServerPort = port;
            MaxClients = maxClients;
        }

        public void StartServer()
        {
            BufferManager = new BufferProvider(MaxClients, ClientSendBufsize, MaxClients, ClientReceiveBufsize);

            ServerSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            ServerSocket.NoDelay = NaggleNoDelay;
            ServerSocket.ReceiveBufferSize = SoocketReceiveBufferSize;
            ServerSocket.Bind(new IPEndPoint(IPAddress.Any, ServerPort));
            ServerSocket.Listen(MaxClients);


            SocketAsyncEventArgs e = new SocketAsyncEventArgs();
            e.Completed += Accepted;
            if (!ServerSocket.AcceptAsync(e))
            {
                Accepted(null, e);
            }
        }


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
            return true;
        }

        protected virtual IAsyncSession CreateSession(SocketAsyncEventArgs e, Guid sessionId, BufferProvider bufferManager)
        {
            var session = new TcpSession(e, sessionId, bufferManager);
            session.socketSendBufferSize = ClientSendBufsize;
            session.socketRecieveBufferSize = ClientReceiveBufsize;
            session.maxIndexedMemory = MaxIndexedMemoryPerClient;
            session.dropOnCongestion = DropOnBackPressure;
            return session;
        }


        public void SendBytesToAllClients(byte[] bytes)
        {
            Parallel.ForEach(Sessions, session =>
            {
                session.Value.SendAsync(bytes);
            });
        }

        public void SendBytesToClient(Guid id, byte[] bytes)
        {
            Sessions[id].SendAsync(bytes);
        }

        #region Virtual

        protected virtual void HandleBytesRecieved(Guid guid, byte[] bytes, int offset, int count)
        {
            OnBytesRecieved?.Invoke(guid, bytes, offset, count);
        }

        protected virtual void HandleClientAccepted(Guid clientGuid, SocketAsyncEventArgs e)
        {
            OnClientAccepted?.Invoke(clientGuid, e.AcceptSocket);
        }
        protected virtual void HandleDeadSession(Guid sessionId)
        {
            Sessions.TryRemove(sessionId, out _);
        }

        protected virtual void HandleError(SocketError error, string context)
        {
            MiniLogger.Log(MiniLogger.LogLevel.Error, context + Enum.GetName(typeof(SocketError), error));
        }
        public virtual void StopServer()
        {
            Stopping = true;
            ServerSocket.Close();
            ServerSocket.Dispose();

            foreach (var session in Sessions)
            {
                session.Value.EndSession();
            }

        }

        #endregion
    }


}
