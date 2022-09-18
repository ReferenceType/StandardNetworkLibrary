using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkLibrary.UDP
{
    public class AsyncUdpClient
    {
        public delegate void BytesRecieved(byte[] bytes, int offset, int count);

        public BytesRecieved OnBytesRecieved;
        public Action OnConnected;
        private Socket clientSocket;
        public bool Connected { get; private set; } = false;
        private EndPoint remoteEndPoint;

        public int ReceiveBufferSize = 1280000;
        public int SocketSendBufferSize = 128000;

        protected byte[] recieveBuffer;

        public AsyncUdpClient()
        {
            recieveBuffer = new byte[ReceiveBufferSize];
            clientSocket = new Socket(SocketType.Dgram, ProtocolType.Udp);
            clientSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, false);
            clientSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            clientSocket.ReceiveBufferSize = ReceiveBufferSize;
            clientSocket.SendBufferSize = SocketSendBufferSize;
            remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        }

        public AsyncUdpClient(int port) : this()
        {
            var bindPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), port);
            clientSocket.Bind(bindPoint);
        }

        public void SetRemoteEnd(string ip, int port)
        {
            remoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            Receive();

        }
        public void Connect(string IP, int port)
        {
            remoteEndPoint = new IPEndPoint(IPAddress.Parse(IP), port);
            clientSocket.Connect(remoteEndPoint);
            Connected = true;
            clientSocket.BeginReceive(recieveBuffer, 0, recieveBuffer.Length, SocketFlags.None, EndRecieve, null);
        }

        #region Receive
        private void Receive()
        {
            if (clientSocket.Connected)
                clientSocket.BeginReceive(recieveBuffer, 0, recieveBuffer.Length, SocketFlags.None, EndRecieve, null);
            else
                clientSocket.BeginReceiveFrom(recieveBuffer, 0, recieveBuffer.Length, SocketFlags.None, ref remoteEndPoint, EndRecieveFrom, null);

        }
        private void EndRecieve(IAsyncResult ar)
        {
            int amount = clientSocket.EndReceive(ar);
            HandleBytesReceived(recieveBuffer, 0, amount);

            if (ar.CompletedSynchronously)
                ThreadPool.UnsafeQueueUserWorkItem((e) => Receive(), null);
            else
                Receive();
        }
        private void EndRecieveFrom(IAsyncResult ar)
        {
            int amount = clientSocket.EndReceiveFrom(ar, ref remoteEndPoint);
            HandleBytesReceived(recieveBuffer, 0, amount);

            if (ar.CompletedSynchronously)
                ThreadPool.UnsafeQueueUserWorkItem((e) => Receive(), null);
            else
                Receive();
        }
        #endregion

        protected virtual void HandleBytesReceived(byte[] buffer, int offset, int count)
        {
            OnBytesRecieved?.Invoke(buffer, offset, count);
        }


        public void SendAsync(byte[] bytes)
        {
            if (clientSocket.Connected)
                clientSocket.BeginSend(bytes, 0, bytes.Length, SocketFlags.None, EndSend, null);
            else
                clientSocket.BeginSendTo(bytes, 0, bytes.Length, SocketFlags.None, remoteEndPoint, EndSendTo, null);
        }

        private void EndSend(IAsyncResult ar)
        {
            clientSocket.EndSend(ar);
        }

        private void EndSendTo(IAsyncResult ar)
        {
            clientSocket.EndSendTo(ar);
        }

        public void JoinMulticastGroup(IPAddress multicastAddr)
        {
            MulticastOption mcOpt = new MulticastOption(multicastAddr, ((IPEndPoint)clientSocket.LocalEndPoint).Address);
            clientSocket.SetSocketOption(
                SocketOptionLevel.IP,
                SocketOptionName.AddMembership,
                mcOpt);
        }

    }
}
