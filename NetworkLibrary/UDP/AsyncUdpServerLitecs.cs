using NetworkLibrary.Components.Statistics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace NetworkLibrary.UDP
{
    public class AsyncUdpServerLite
    {
        public delegate void ClientAccepted(SocketAsyncEventArgs ClientSocket);
        public delegate void BytesRecieved(IPEndPoint adress, byte[] bytes, int offset, int count);
        public ClientAccepted OnClientAccepted;
        public BytesRecieved OnBytesRecieved;
        public int ClientReceiveBufferSize = 65000;

        public int SocketReceiveBufferSize
        {
            get => receiveBufferSize;
            set
            {
                ServerSocket.ReceiveBufferSize = value;
                receiveBufferSize = value;
            }
        }
        public int SocketSendBufferSize
        {
            get => socketSendBufferSize;
            set
            {
                ServerSocket.SendBufferSize = value;
                socketSendBufferSize = value;
            }
        }
        public EndPoint LocalEndpoint => ServerSocket.LocalEndPoint;
        private int receiveBufferSize = 1280000000;
        private int socketSendBufferSize = 1280000000;
        protected Socket ServerSocket;
      
        protected int port = 0;

        protected EndPoint serverEndpoint;
        protected EndPoint multicastEndpoint;

        public AsyncUdpServerLite(int port = 20008)
        {
            // IPV6 is not compatible with Unity.
            ServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            ServerSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, true);
            // Not compatible with Unity..
            //ServerSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.PacketInformation, true);


            ServerSocket.ReceiveBufferSize = SocketReceiveBufferSize;
            ServerSocket.SendBufferSize = SocketSendBufferSize;

            serverEndpoint = new IPEndPoint(IPAddress.Any, port);
            ServerSocket.Bind(serverEndpoint);
            ServerSocket.Blocking = false;
            this.port = port;
        }
       
      
        public void StartServer()
        {
            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                StartReceiveSentinel();
            }
        }

        private void StartReceiveSentinel()
        {
            var e = new SocketAsyncEventArgs();
            e.Completed += Received;
            e.SetBuffer(new byte[ClientReceiveBufferSize], 0, ClientReceiveBufferSize);
            e.RemoteEndPoint = serverEndpoint;

            Receive(e);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Receive(SocketAsyncEventArgs e)
        {
            if (!ServerSocket.ReceiveFromAsync(e))
            {
                ThreadPool.UnsafeQueueUserWorkItem((cb) => Received(null, e), null);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Received(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                StartReceiveSentinel();
                e.Dispose();
                return;
            }

            HandleMessage(e);
            e.RemoteEndPoint = serverEndpoint;
            e.SetBuffer(0, ClientReceiveBufferSize);
            Receive(e);
        }

        private void HandleMessage(SocketAsyncEventArgs e)
        {
            var clientRemoteEndpoint = (IPEndPoint)e.RemoteEndPoint;
            HandleBytesReceived(clientRemoteEndpoint, e.Buffer, e.Offset, e.BytesTransferred);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void HandleBytesReceived(IPEndPoint clientRemoteEndpoint, byte[] buffer, int offset, int count)
        {
            OnBytesRecieved?.Invoke(clientRemoteEndpoint, buffer, offset, count);
        }
      
        public void SendBytesToClient(IPEndPoint clientEndpoint, byte[] bytes, int offset, int count)
        {

            try
            {
                ServerSocket.SendTo(bytes, offset, count, SocketFlags.None, clientEndpoint);
            }
            catch
            {
                
            }
        }

       

     
    }

}
