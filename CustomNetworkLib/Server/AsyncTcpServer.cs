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
            public delegate void BytesRecieved(Guid guid, byte[] bytes);
            public ClientAccepted OnClientAccepted;
            public BytesRecieved OnBytesRecieved;

            protected Socket ServerSocket;
           
            
            ConcurrentDictionary<Guid,IAsyncSession> Sessions = new ConcurrentDictionary<Guid, IAsyncSession>();
            public AsyncTcpServer(int port = 20008)
            {
                ServerSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                //ServerSocket.NoDelay = true;
                ServerSocket.ReceiveBufferSize = 1280000;
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
                if (acceptedArg.SocketError != SocketError.Success)
                {
                    Console.WriteLine(Enum.GetName(typeof(SocketError), acceptedArg.SocketError));
                    return;
                }
                int port = ((IPEndPoint)acceptedArg.AcceptSocket.RemoteEndPoint).Port;
                Guid clientGuid = Guid.NewGuid();

                var ses = CreateSession(acceptedArg, clientGuid);


                ses.OnBytesRecieved += (object sndr, byte[] bytes) => { HandleBytesRecieved(clientGuid, bytes); };
                Sessions.TryAdd(clientGuid, ses);
                
                HandleClientAccepted(clientGuid,acceptedArg);
                Console.WriteLine("Accepted with port: " + ((IPEndPoint)acceptedArg.AcceptSocket.RemoteEndPoint).Port);


                SocketAsyncEventArgs nextClient = new SocketAsyncEventArgs();
                nextClient.Completed += Accepted;

                if (!ServerSocket.AcceptAsync(nextClient))
                {
                    Accepted(null, nextClient);
                }

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

         

            protected virtual void HandleBytesRecieved(Guid guid,byte[] bytes)
            {
                OnBytesRecieved.Invoke(guid, bytes);
            }

            protected virtual void HandleClientAccepted(Guid clientGuid, SocketAsyncEventArgs e)
            {
                OnClientAccepted?.Invoke(clientGuid, e.AcceptSocket);
            }

           
            #endregion
        }


    }

}
