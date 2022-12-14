using NetworkLibrary.Components.Statistics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Schema;

namespace NetworkLibrary.UDP
{

    public class AsyncUdpServer
    {
        public delegate void ClientAccepted(SocketAsyncEventArgs ClientSocket);
        public delegate void BytesRecieved(IPEndPoint adress, byte[] bytes, int offset, int count);
        public ClientAccepted OnClientAccepted;
        public BytesRecieved OnBytesRecieved;

        protected Socket ServerSocket;

        protected ConcurrentDictionary<IPEndPoint, SocketAsyncEventArgs> RegisteredClients = new ConcurrentDictionary<IPEndPoint, SocketAsyncEventArgs>();
        protected ConcurrentDictionary<IPEndPoint, UdpStatistics> Statistics = new ConcurrentDictionary<IPEndPoint, UdpStatistics>();

        protected int port = 0;
        private UdpStatisticsPublisher statisticsPublisher;
        protected EndPoint serverEndpoint;
        protected EndPoint multicastEndpoint;

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

        private int receiveBufferSize = 1280000000;
        private int socketSendBufferSize = 1280000000;

        public int ClientReceiveBufferSize = 65000;

       


        public AsyncUdpServer(int port = 20008)
        {
            ServerSocket = new Socket(SocketType.Dgram, ProtocolType.Udp);
            ServerSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, true);

            ServerSocket.ReceiveBufferSize = SocketReceiveBufferSize;
            ServerSocket.SendBufferSize = SocketSendBufferSize;

            serverEndpoint = new IPEndPoint(IPAddress.Any, port);
            ServerSocket.Bind(serverEndpoint);
            ServerSocket.Blocking = false;
            this.port = port;
            statisticsPublisher = new UdpStatisticsPublisher(Statistics);
        }
        public void GetStatistics(out UdpStatistics generalStats, out ConcurrentDictionary<IPEndPoint, UdpStatistics> sessionStats)
        {
            statisticsPublisher.GetStatistics(out generalStats,out sessionStats);
        }
        // 239.0.0.0 to 239.255.255.255
        public void SetMulticastAddress(string Ip, int port) => multicastEndpoint = new IPEndPoint(IPAddress.Parse(Ip), port);


        public void StartServer()
        {
            var e = new SocketAsyncEventArgs();
            e.Completed += Received;
            e.SetBuffer(new byte[ClientReceiveBufferSize], 0, ClientReceiveBufferSize);
            e.RemoteEndPoint = serverEndpoint;

            Receive(e);
        }

        private void Receive(SocketAsyncEventArgs e)
        {
            if (!ServerSocket.ReceiveFromAsync(e))
            {
                ThreadPool.QueueUserWorkItem((cb) => Received(null, e));
            }
        }

        private void Received(object sender, SocketAsyncEventArgs e)
        {
            HandleMessage(e);
            e.RemoteEndPoint = serverEndpoint;
            e.SetBuffer(0, ClientReceiveBufferSize);
            Receive(e);
        }

        private void HandleMessage(SocketAsyncEventArgs e)
        {
            var clientRemoteEndpoint = e.RemoteEndPoint as IPEndPoint;
            if (RegisteredClients.TryAdd(clientRemoteEndpoint, null))
            {
                HandleClientRegistered(e);
            }

            HandleBytesReceived(clientRemoteEndpoint, e.Buffer, e.Offset, e.BytesTransferred);
        }

        private void HandleClientRegistered(SocketAsyncEventArgs acceptedArg)
        {
            var endpoint = acceptedArg.RemoteEndPoint as IPEndPoint;
#if Debug
            Console.WriteLine(" new client arrivd registering IP:" + endpoint.Address + "Port: " + endpoint.Port);
#endif
            // we try to scale the recieve side here
            var e = new SocketAsyncEventArgs();
            e.Completed += RecievedFromKnownEndpoint;
            e.SetBuffer(new byte[ClientReceiveBufferSize], 0, ClientReceiveBufferSize);
            e.RemoteEndPoint = endpoint;

            Statistics[endpoint] = new UdpStatistics();

            ReceiveFromKnownClient(e);
            OnClientAccepted?.Invoke(acceptedArg);

        }

        private void ReceiveFromKnownClient(SocketAsyncEventArgs e)
        {
            if (!ServerSocket.ReceiveFromAsync(e))
            {
                ThreadPool.UnsafeQueueUserWorkItem((cb) => RecievedFromKnownEndpoint(null, e), null);
            }
        }

        private void RecievedFromKnownEndpoint(object sender, SocketAsyncEventArgs e)
        {
            HandleMessage(e);
            e.SetBuffer(0, e.Buffer.Length);
            ReceiveFromKnownClient(e);
        }

        void HandleBytesReceived(IPEndPoint clientRemoteEndpoint, byte[] buffer, int offset, int count)
        {
            if (Statistics.TryGetValue(clientRemoteEndpoint, out var stats))
            {
                stats.TotalBytesReceived += count;
                stats.TotalDatagramReceived += 1;
            }
           
            OnBytesRecieved?.Invoke(clientRemoteEndpoint, buffer, offset, count);


        }
        public void SendBytesToAllClients(byte[] bytes)
        {
            foreach (var client in RegisteredClients)
            {
                SendBytesToClient(client.Key, bytes, 0, bytes.Length);
            }
        }

        public void SendBytesToClient(IPEndPoint clientEndpoint, byte[] bytes, int offset, int count)
        {
            
            try
            {
                ServerSocket.SendTo(bytes, offset, count, SocketFlags.None, clientEndpoint);
                Statistics[clientEndpoint].TotalBytesSent += count;
                Statistics[clientEndpoint].TotalDatagramSent +=1;
            }
            catch 
            {
                Statistics[clientEndpoint].TotalMessageDropped+=1;

            }
        }

        public void RemoveClient(IPEndPoint endPoint)
        {
            RegisteredClients.TryRemove(endPoint, out var client);
        }

        public void RemoveAllClients()
        {
            RegisteredClients = new ConcurrentDictionary<IPEndPoint, SocketAsyncEventArgs>();
        }

        public void MulticastMessage(byte[] message)
        {
            if (multicastEndpoint != null)
                ServerSocket.BeginSendTo(message, 0, message.Length, SocketFlags.None, multicastEndpoint, (ar) => ServerSocket.EndSendTo(ar), null);

        }

    }
}
