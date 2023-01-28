using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkLibrary.UDP
{
    public class AsyncUdpClient : IDisposable
    {
        public delegate void BytesRecieved(byte[] bytes, int offset, int count);

        public BytesRecieved OnBytesRecieved;

        public Action<Exception> OnError;
        public Action OnConnected;
        private Socket clientSocket;

        public bool Connected { get; private set; } = false;
        public int ReceiveBufferSize
        {
            get => receiveBufferSize;
            set
            {
                clientSocket.ReceiveBufferSize = value; receiveBufferSize = value;
            }
        }
        public int SocketSendBufferSize
        {
            get => socketSendBufferSize;
            set
            {
                clientSocket.SendBufferSize = value; socketSendBufferSize = value;
            }
        }

        public EndPoint LocalEndpoint => clientSocket.LocalEndPoint;

        public EndPoint RemoteEndPoint { get => remoteEndPoint; private set => remoteEndPoint = value; }

        private int receiveBufferSize = 12800000;
        private int socketSendBufferSize = 128000000;

        protected byte[] recieveBuffer;
        private IAsyncResult activeRec;
        private EndPoint remoteEndPoint;

        public AsyncUdpClient()
        {
            recieveBuffer = new byte[65500];
            clientSocket = new Socket(SocketType.Dgram, ProtocolType.Udp);

            clientSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, false);
            //clientSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            clientSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.PacketInformation, true);

            clientSocket.ReceiveBufferSize = ReceiveBufferSize;
            clientSocket.SendBufferSize = SocketSendBufferSize;
            clientSocket.Blocking = false;

            RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        }

        public AsyncUdpClient(int port) : this()
        {
            var bindPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), port);
            clientSocket.Bind(bindPoint);
        }

        public void Bind()
        {
            var bindPoint = new IPEndPoint(IPAddress.Any, 0);
            clientSocket.Bind(bindPoint);

        }

        public void Bind(int port)
        {
            var bindPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), port);
            clientSocket.Bind(bindPoint);

        }
        public void SetRemoteEnd(string ip, int port, bool receive = true)
        {
            if (IPAddress.TryParse(ip, out var ipAdress))
            {
                RemoteEndPoint = new IPEndPoint(ipAdress, port);
                if (receive)
                    Receive();
            }


        }
        public void Connect(IPEndPoint ep)
        {
            var ip = ep.Address.ToString();
            var port = ep.Port;
            Connect(ip, port);
        }
        public void Connect(string IP, int port)
        {
            RemoteEndPoint = new IPEndPoint(IPAddress.Parse(IP), port);
            clientSocket.Connect(RemoteEndPoint);
            Connected = true;
            clientSocket.Blocking = false;
            clientSocket.BeginReceive(recieveBuffer, 0, recieveBuffer.Length, SocketFlags.None, EndRecieve, null);
        }

        #region Receive
        private void Receive()
        {
            if (clientSocket.Connected)
            {
                try
                {
                    clientSocket.BeginReceive(recieveBuffer, 0, recieveBuffer.Length, SocketFlags.None, EndRecieve, null);

                }
                catch (Exception e)
                {
                    OnError?.Invoke(e);
                }
            }

            else
            {
                try
                {
                    activeRec = clientSocket.BeginReceiveFrom(recieveBuffer, 0, recieveBuffer.Length, SocketFlags.None, ref remoteEndPoint, EndRecieveFrom, null);

                }
                catch (Exception e)
                {
                    OnError?.Invoke(e);

                }

            }

        }
        private void EndRecieve(IAsyncResult ar)
        {
            int amount = 0;
            try
            {
                amount = clientSocket.EndReceive(ar);
            }
            catch (SocketException e)
            {
                OnError?.Invoke(e);
                return;
            };
            HandleBytesReceived(recieveBuffer, 0, amount);

            if (ar.CompletedSynchronously)
                ThreadPool.UnsafeQueueUserWorkItem((e) => Receive(), null);
            else
                Receive();
        }
        private void EndRecieveFrom(IAsyncResult ar)
        {
            int amount = 0;
            try
            {
                amount = clientSocket.EndReceiveFrom(ar, ref remoteEndPoint);
            }
            catch (Exception e)
            {
                OnError?.Invoke(e);
                return;
            };
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


        public virtual void SendAsync(byte[] bytes, int offset, int count)
        {
            try
            {
                clientSocket.SendTo(bytes, offset, count, SocketFlags.None, RemoteEndPoint);

            }
            catch (Exception e)
            {
            }
        }
        public void SendAsync(byte[] bytes)
        {
            SendAsync(bytes, 0, bytes.Length);
        }



        public void JoinMulticastGroup(IPAddress multicastAddr)
        {
            MulticastOption mcOpt = new MulticastOption(multicastAddr, ((IPEndPoint)clientSocket.LocalEndPoint).Address);
            clientSocket.SetSocketOption(
                SocketOptionLevel.IP,
                SocketOptionName.AddMembership,
                mcOpt);
        }

        public void Dispose()
        {
            clientSocket.Close();
            clientSocket.Dispose();
        }
    }
}
