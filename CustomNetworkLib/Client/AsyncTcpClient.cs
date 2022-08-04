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

namespace CustomNetworkLib
{
    public class AsyncTpcClient
    {

        public Action<byte[]> OnBytesRecieved;

       
        public Action OnConnected;
        public Action OnConnectFailed;
        public Action OnDisconnected;
        

        private Socket clientSocket;
        private TaskCompletionSource<bool> connectedCompletionSource;
        
        protected bool connected = false;
        private string IP;
        private int port;
        protected IAsyncSession session;

        #region Configuration
        //256000
        public int SocketSendBufferSize = 256000;
        public int SocketRecieveBufferSize = 256000;

        public int MessageSendBufferSize = 256000;
        public int MessageRecieveBufferSize = 256000;
        public int MaxMessageSize = 100000000;

        #endregion

        public AsyncTpcClient()
        {}

        public async Task<bool> ConnectAsyncAwaitable(string IP, int port)
        {
            connectedCompletionSource = new TaskCompletionSource<bool>();
            ConnectAsync(IP, port);
            return await connectedCompletionSource.Task;
        }

        public void ConnectAsync(string IP, int port)
        {
            this.IP = IP;
            this.port = port;

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
                Console.WriteLine(Enum.GetName(typeof(SocketError), e.SocketError));
                //HandleError(e, "While connecting an error occured: ");

                OnConnectFailed?.Invoke();
                connectedCompletionSource?.SetException(new SocketException((int)e.SocketError));
            }
            else
            {
                e.AcceptSocket = e.ConnectSocket;
                
                
                Console.WriteLine("Connected with port " + ((IPEndPoint)e.ConnectSocket.LocalEndPoint).Port);

                HandleConnected(e);
               

                connectedCompletionSource?.SetResult(true);
                connected = true;
            }
        }

        public virtual void SendAsync(byte[] buffer)
        {
            if (connected)
                session.SendAsync(buffer);
            return;
          
        }

       

        private void HandleError(SocketAsyncEventArgs e, string context)
        {
            Console.WriteLine("An error Occured while " + context + " associated port: "
                + ((IPEndPoint)e.AcceptSocket.RemoteEndPoint).Port + " Error: " + Enum.GetName(typeof(SocketError), e.SocketError));
            try { DisconnectClient(e); } catch { }

            //// Skip disconnect errors
            //if ((error == SocketError.ConnectionAborted) ||
            //    (error == SocketError.ConnectionRefused) ||
            //    (error == SocketError.ConnectionReset) ||
            //    (error == SocketError.OperationAborted) ||
            //    (error == SocketError.Shutdown))
            //    return;

            //OnError(error);
        }

        private void DisconnectClient(SocketAsyncEventArgs e)
        {
            //try
            //{
            //    Console.WriteLine("Disconnecting");
            //    int port = ((IPEndPoint)e.AcceptSocket.RemoteEndPoint).Port;

            //    clientSocketRecieveArgs.Completed += ClientDisconnected;
            //    try
            //    {
            //        clientSocketRecieveArgs.AcceptSocket.Shutdown(SocketShutdown.Both);
            //    }
            //    catch { };

            //    if (!clientSocketRecieveArgs.AcceptSocket.DisconnectAsync(clientSocketRecieveArgs))
            //    {
            //        ClientDisconnected(null, clientSocketRecieveArgs);
            //    }

            //    clientSocketSendArgs.Dispose();
            //    try
            //    {
            //        clientSocketSendArgs.AcceptSocket.Shutdown(SocketShutdown.Both);
            //        if (!clientSocketSendArgs.AcceptSocket.DisconnectAsync(clientSocketSendArgs))
            //        {
            //            ClientDisconnected(null, clientSocketSendArgs);
            //        }
            //    }
            //    catch { };
            //}
            //catch { }
            //finally
            //{

            //}




        }


        private void ClientDisconnected(object sender, SocketAsyncEventArgs e)
        {
            e.AcceptSocket.Close();
            e.AcceptSocket.Dispose();
            e.Dispose();
        }


        protected virtual void HandleConnected(SocketAsyncEventArgs e)
        {
            CreateSession(e,Guid.NewGuid());
            session.OnBytesRecieved += (object snder, byte[] bytes) => HandleBytesRecieved(bytes);
            OnConnected?.Invoke();
        }
        protected virtual void CreateSession(SocketAsyncEventArgs e, Guid sessionId )
        {
            session = new TcpSession(e,sessionId);
        }

        protected virtual void HandleBytesRecieved(byte[] bytes)
        {
             OnBytesRecieved?.Invoke(bytes);
        }

    }
}
