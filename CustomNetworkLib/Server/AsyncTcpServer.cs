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
            // todo make the action
            public delegate void ClientAccepted(Guid guid, Socket ClientSocket);
            public delegate void BytesRecieved(Guid guid, byte[] bytes, int offset, int count);
            public ClientAccepted OnClientAccepted;
            public BytesRecieved OnBytesRecieved;

            protected Socket ServerSocket;
           
            
            public ConcurrentDictionary<Guid,IAsyncSession> Sessions = new ConcurrentDictionary<Guid, IAsyncSession>();
            public AsyncTcpServer(int port = 20008)
            {
                ServerSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                //ServerSocket.NoDelay = true;
                ServerSocket.ReceiveBufferSize = 2080000000;
                ServerSocket.Bind(new IPEndPoint(IPAddress.Any, port));
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
                SocketAsyncEventArgs nextClient = new SocketAsyncEventArgs();
                nextClient.Completed += Accepted;

                if (!ServerSocket.AcceptAsync(nextClient))
                {
                    Task.Run(() => Accepted(null, nextClient));
                }
                if (acceptedArg.SocketError != SocketError.Success)
                {
                    Console.WriteLine(Enum.GetName(typeof(SocketError), acceptedArg.SocketError));
                    return;
                }
                int port = ((IPEndPoint)acceptedArg.AcceptSocket.RemoteEndPoint).Port;
                Guid clientGuid = Guid.NewGuid();

                var session = CreateSession(acceptedArg, clientGuid);


                session.OnBytesRecieved += (byte[] bytes,int offset, int count) => { HandleBytesRecieved(clientGuid, bytes,offset,count); };
                Sessions.TryAdd(clientGuid, session);
                
                HandleClientAccepted(clientGuid,acceptedArg);
                Console.WriteLine("Accepted with port: " + ((IPEndPoint)acceptedArg.AcceptSocket.RemoteEndPoint).Port);
            }

            protected virtual IAsyncSession CreateSession(SocketAsyncEventArgs e, Guid sessionId)
            {
                return new TcpSession(e, sessionId);
            }

            //#region Send
            public void SendBytesToAllClients(byte[] bytes)
            {
                foreach (var item in Sessions)
                {
                    item.Value.SendAsync(bytes);
                }
            }

            public void SendBytesToClient(Guid id, byte[] bytes)
            {
                Sessions[id].SendAsync(bytes);
            }

            #region Virtual

            protected virtual void HandleBytesRecieved(Guid guid,byte[] bytes, int offset, int count)
            {
                OnBytesRecieved.Invoke(guid, bytes,offset,count);
            }

            protected virtual void HandleClientAccepted(Guid clientGuid, SocketAsyncEventArgs e)
            {
                OnClientAccepted?.Invoke(clientGuid, e.AcceptSocket);
            }

           
            #endregion
        }


    }

}
