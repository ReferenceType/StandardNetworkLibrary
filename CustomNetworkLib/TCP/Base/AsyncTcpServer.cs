using System;

namespace CustomNetworkLib
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;


    namespace SocketEventArgsTests
    {
        public class AsyncTcpServer
        {
            public delegate void ClientAccepted(Guid guid, Socket ClientSocket);
            public delegate void BytesRecieved(Guid guid, byte[] bytes, int offset, int count);
            public ClientAccepted OnClientAccepted;
            public BytesRecieved OnBytesRecieved;

            public int MaxIndexedMemoryPerClient = 128000;
            public int SoocketReceiveBufferSize = 2080000000;
            public bool DropOnBackPressure=false;
            public bool NaggleNoDelay = false;

            protected Socket ServerSocket;
            protected int ServerPort { get; }
            public ConcurrentDictionary<Guid, IAsyncSession> Sessions { get; } = new ConcurrentDictionary<Guid, IAsyncSession>();
            public AsyncTcpServer(int port = 20008)
            {
                ServerPort = port;
            }

            public void StartServer()
            {
                ServerSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                ServerSocket.NoDelay = NaggleNoDelay;
                ServerSocket.ReceiveBufferSize = SoocketReceiveBufferSize;
                ServerSocket.Bind(new IPEndPoint(IPAddress.Any, ServerPort));
                ServerSocket.Listen(10000);


                SocketAsyncEventArgs e = new SocketAsyncEventArgs();
                e.Completed += Accepted;
                if (!ServerSocket.AcceptAsync(e))
                {
                    Accepted(null, e);
                }
            }


            private void Accepted(object sender, SocketAsyncEventArgs acceptedArg)
            {
                if (acceptedArg.SocketError != SocketError.Success)
                {
                    HandleError(acceptedArg.SocketError, "While Accepting Client an error occured");
                    return;
                }
                SocketAsyncEventArgs nextClient = new SocketAsyncEventArgs();
                nextClient.Completed += Accepted;

                if (!ServerSocket.AcceptAsync(nextClient))
                {
                    ThreadPool.QueueUserWorkItem((s) => Accepted(null, nextClient));
                }

                if (acceptedArg.SocketError != SocketError.Success)
                {
                    Console.WriteLine(Enum.GetName(typeof(SocketError), acceptedArg.SocketError));
                    return;
                }

                int port = ((IPEndPoint)acceptedArg.AcceptSocket.RemoteEndPoint).Port;
                Guid clientGuid = Guid.NewGuid();

                var session = CreateSession(acceptedArg, clientGuid);
                Sessions.TryAdd(clientGuid, session);

                session.OnBytesRecieved += (byte[] bytes,int offset, int count) => { HandleBytesRecieved(clientGuid, bytes,offset,count); };
                session.OnSessionClosed += (Guid sessionId) => { HandleDeadSession(sessionId); };

                session.StartSession();
                Console.WriteLine("Accepted with port: " + ((IPEndPoint)acceptedArg.AcceptSocket.RemoteEndPoint).Port);

                HandleClientAccepted(clientGuid,acceptedArg);
            }


            protected virtual IAsyncSession CreateSession(SocketAsyncEventArgs e, Guid sessionId)
            {
                var session = new TcpSession(e, sessionId);
                session.MaxIndexedMemory = MaxIndexedMemoryPerClient;
                session.DropOnCongestion = DropOnBackPressure;
                return session;
            }


            //#region Send
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

            protected virtual void HandleBytesRecieved(Guid guid,byte[] bytes, int offset, int count)
            {
                OnBytesRecieved?.Invoke(guid, bytes,offset,count);
            }

            protected virtual void HandleClientAccepted(Guid clientGuid, SocketAsyncEventArgs e)
            {
                OnClientAccepted?.Invoke(clientGuid, e.AcceptSocket);
            }
            protected virtual void HandleDeadSession(Guid sessionId)
            {
                Sessions.TryRemove(sessionId, out _);
            }

            protected virtual void HandleError(SocketError error, object context)
            {

            }
            public void StopServer()
            {
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

}
