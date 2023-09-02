using NetworkLibrary.Utils;
using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;

namespace NetworkLibrary.UDP
{
    public class AsyncUdpServerLite
    {
        public delegate void BytesRecieved(IPEndPoint adress, byte[] bytes, int offset, int count);
        public BytesRecieved OnBytesRecieved;
        public int ClientReceiveBufferSize = 65000;
        bool isConnected = false;
        IPEndPoint adress;
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
        private int receiveBufferSize = 256000000;
        private int socketSendBufferSize = 256000000;
        protected internal Socket ServerSocket;

        protected int port = 0;

        protected EndPoint serverEndpoint;
        protected EndPoint multicastEndpoint;

        public AsyncUdpServerLite(int port)
        {
            // IPV6 is not compatible with Unity.
            ServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            ServerSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, true);
            // Not compatible with Unity..
            //ServerSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.PacketInformation, true);


            ServerSocket.ReceiveBufferSize = SocketReceiveBufferSize;
            ServerSocket.SendBufferSize = SocketSendBufferSize;

            serverEndpoint = new IPEndPoint(IPAddress.Any, port);
            try
            {
                ServerSocket.Bind(serverEndpoint);
            }
            catch 
            {
                MiniLogger.Log(MiniLogger.LogLevel.Warning, $"Unable to bind udp port {port} choosing default");
                serverEndpoint = new IPEndPoint(IPAddress.Any, 0);
                ServerSocket.Bind(serverEndpoint);
            }
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
        public void StartAsClient(IPEndPoint ep)
        {
            ServerSocket.Connect(ep);
            adress = ep;
            isConnected = true;
            StartReceiveSentinel2();
        }
        #region Unconnected Logic
        private void StartReceiveSentinel()
        {
            var e = new SocketAsyncEventArgs();
            e.Completed += Received;
            e.SetBuffer(ByteCopy.GetNewArray(ClientReceiveBufferSize, true), 0, ClientReceiveBufferSize);
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

            //HandleMessage(e);
            var clientRemoteEndpoint = e.RemoteEndPoint as IPEndPoint;
            OnBytesRecieved?.Invoke(clientRemoteEndpoint, e.Buffer, e.Offset, e.BytesTransferred);

            e.RemoteEndPoint = serverEndpoint;
            e.SetBuffer(0, ClientReceiveBufferSize);
            Receive(e);
        }
        #endregion

        #region Connected Logic
        private void StartReceiveSentinel2()
        {
            var e = new SocketAsyncEventArgs();
            e.Completed += Received2;
            e.SetBuffer(ByteCopy.GetNewArray(ClientReceiveBufferSize, true), 0, ClientReceiveBufferSize);

            Receive2(e);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Receive2(SocketAsyncEventArgs e)
        {
            if (!ServerSocket.ReceiveAsync(e))
            {
                ThreadPool.UnsafeQueueUserWorkItem((cb) => Received2(null, e), null);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Received2(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                StartReceiveSentinel2();
                e.Dispose();
                return;
            }

            //HandleMessage(e);
            var clientRemoteEndpoint = e.RemoteEndPoint as IPEndPoint;
            OnBytesRecieved?.Invoke(clientRemoteEndpoint, e.Buffer, e.Offset, e.BytesTransferred);

            e.SetBuffer(0, ClientReceiveBufferSize);
            Receive2(e);
        }
        #endregion

        private void HandleMessage(SocketAsyncEventArgs e)
        {
            var clientRemoteEndpoint = e.RemoteEndPoint as IPEndPoint;
            // HandleBytesReceived(clientRemoteEndpoint, e.Buffer, e.Offset, e.BytesTransferred);
            OnBytesRecieved?.Invoke(clientRemoteEndpoint, e.Buffer, e.Offset, e.BytesTransferred);

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
                if (isConnected)
                    ServerSocket.Send(bytes, offset, count, SocketFlags.None);
                else
                    ServerSocket.SendTo(bytes, offset, count, SocketFlags.None, clientEndpoint);
            }
            catch (Exception e)
            {
                MiniLogger.Log(MiniLogger.LogLevel.Error, "Failed To Send Udp Message " + e.Message);
            }
        }

    }

}
