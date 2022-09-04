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
        public Action<byte[],int,int> OnBytesRecieved;
        public Action OnConnected;
        public Action OnConnectFailed;
        public Action OnDisconnected;
        

        private Socket clientSocket;
        private TaskCompletionSource<bool> connectedCompletionSource;
        
        protected bool connected = false;
        protected IAsyncSession session;

        
        
        public int SocketSendBufferSize = 128000;
        public int SocketRecieveBufferSize = 128000;
       

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
                HandleError(e, "While connecting an error occured: ");
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
            return;
          
        }

        private void HandleError(SocketAsyncEventArgs e, string context)
        {
            Console.WriteLine("An error Occured while " + context + " associated port: "
                + ((IPEndPoint)e.AcceptSocket.RemoteEndPoint).Port + " Error: " + Enum.GetName(typeof(SocketError), e.SocketError));
            

          
        }

        public void Disconnect()
        {
            session.EndSession();
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
            session.OnBytesRecieved += (byte[] bytes,int offset, int count) => HandleBytesRecieved(bytes, offset, count);
            session.OnSessionClosed += (Guid sessionId) => OnDisconnected?.Invoke();

            session.StartSession();
            OnConnected?.Invoke();
        }
        protected virtual void CreateSession(SocketAsyncEventArgs e, Guid sessionId )
        {
            session = new TcpSession(e,sessionId);
        }

        protected virtual void HandleBytesRecieved(byte[] bytes,int offset,int count)
        {
             OnBytesRecieved?.Invoke(bytes,offset,count);
        }

    }
}
