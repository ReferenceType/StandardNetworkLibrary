using CustomNetworkLib;
using CustomNetworkLib.Session.Interface;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetworkSystem
{
    public class AsyncUdpClient
    {
        public delegate void BytesRecieved(byte[] bytes, int offset, int count);

        public BytesRecieved OnBytesRecieved;
        public Action OnConnected;
        private SocketAsyncEventArgs clientSocketRecieveArgs;
        private SocketAsyncEventArgs clientSocketSendArgs;
        private Socket clientSocket;
        private ConcurrentQueue<byte[]> bytesToSend = new ConcurrentQueue<byte[]>();
        private bool conncted = false;
        private string IP;
        private int port;
        IAsyncSession session;

        public AsyncUdpClient()
        {

        }

        public void ConnectAsync(string IP, int port)
        {
            this.IP = IP;
            this.port = port;

            clientSocket = new Socket(SocketType.Dgram, ProtocolType.Udp);
            clientSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, true);
           
            if (clientSocket.AddressFamily == AddressFamily.InterNetworkV6)
                clientSocket.DualMode = true;

            clientSocketRecieveArgs = new SocketAsyncEventArgs();
            clientSocketRecieveArgs.RemoteEndPoint = new IPEndPoint(IPAddress.Parse(IP), port);
            clientSocketRecieveArgs.SetBuffer(new byte[64000], 0, 64000);
            clientSocketRecieveArgs.Completed += Connected;
            clientSocketRecieveArgs.AcceptSocket = clientSocket;

            //await clientSocket.ConnectAsync(IP, port);
            clientSocket.Connect(clientSocketRecieveArgs.RemoteEndPoint);
           
            clientSocketRecieveArgs.AcceptSocket = clientSocket;
            Connected(null, clientSocketRecieveArgs);


            //clientSocket.Bind(new IPEndPoint(IPAddress.Any, 0));
            //Connected(null, clientSocketRecieveArgs);
            
        }

        private void Connected(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                Console.WriteLine(Enum.GetName(typeof(SocketError), e.SocketError));
            }
            else
            {
                e.Completed -= Connected;
                CreateSession(e, Guid.NewGuid());
                session.OnBytesRecieved += ( byte[] bytes,int offset,int count) => OnBytesRecieved?.Invoke(bytes,  offset,  count);
               // Console.WriteLine("Connected with port " + ((IPEndPoint)e.ConnectSocket.LocalEndPoint).Port);
                //OnConnected?.Invoke();

                //e.Completed -= Connected;
                //e.Completed += Recieved;
                //e.SetBuffer(new byte[64000], 0, 64000);
                //e.UserToken = new UserToken(e.ConnectSocket);
                //e.AcceptSocket = e.ConnectSocket;

                //clientSocketSendArgs = new SocketAsyncEventArgs();
                //clientSocketSendArgs.SetBuffer(new byte[64000], 0, 64000);
                //clientSocketSendArgs.UserToken = new UserToken(e.ConnectSocket);
                //clientSocketSendArgs.AcceptSocket = clientSocket;
                //clientSocketSendArgs.RemoteEndPoint = clientSocket.RemoteEndPoint;
                //clientSocketSendArgs.Completed += Sent;

                //conncted = true;
                //if (!clientSocket.ReceiveFromAsync(e))
                //{
                //    Recieved(null, e);
                //}

            }
        }

        protected  virtual void CreateSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            session = new UdpSession(e, sessionId);
        }

        public void SendBytes(byte[] bytes)
        {
            session.SendAsync(bytes);
            return;
            //var token = clientSocketSendArgs.UserToken as UserToken;
            //token.OperationPending.WaitOne();
            //clientSocketSendArgs.SetBuffer(bytes, 0, bytes.Length);
            //if (!clientSocketSendArgs.AcceptSocket.SendToAsync(clientSocketSendArgs))
            //{
            //    Sent(null, clientSocketSendArgs);
            //}
        }

        //private void Sent(object sender, SocketAsyncEventArgs e)
        //{
        //    var token = clientSocketSendArgs.UserToken as UserToken;
        //    token.OperationPending.Set();
        //}

        //private void Recieve(SocketAsyncEventArgs e)
        //{
        //    if (!clientSocket.ReceiveMessageFromAsync(e))
        //    {
        //        Recieved(null, e);
        //    }
        //}
        //private void Recieved(object p, SocketAsyncEventArgs e)
        //{
        //    if (e.SocketError != SocketError.Success)
        //    {
        //        Console.WriteLine(Enum.GetName(typeof(SocketError), e.SocketError));
        //        return;
        //    }
        //    byte[] buffer = new byte[e.BytesTransferred];
        //    Buffer.BlockCopy(e.Buffer, e.Offset, buffer, 0, e.BytesTransferred + e.Offset);

        //    e.SetBuffer(0, e.Buffer.Length);

        //    OnBytesRecieved?.Invoke(buffer);
        //    Recieve(e);

        //}


    }
}
