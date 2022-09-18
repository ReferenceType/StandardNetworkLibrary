using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkLibrary.UDP
{
    public class AsyncUdpServer
    {
        public delegate void ClientAccepted(Socket ClientSocket);
        public delegate void BytesRecieved(IPEndPoint adress, byte[] bytes);
        public ClientAccepted OnClientAccepted;
        public BytesRecieved OnBytesRecieved;

        protected Socket ServerSocket;

        protected ConcurrentDictionary<IPEndPoint, SocketAsyncEventArgs> RegisteredClients = new ConcurrentDictionary<IPEndPoint, SocketAsyncEventArgs>();

        protected int port = 0;
        protected EndPoint serverEndpoint;
        protected EndPoint multicastEndpoint;

        public int SocketSendBufferSize = 12800000;
        public int SocketReceiveBufferSize = 12800000;

        public int ClientReceiveBufferSize = 128000;

        public AsyncUdpServer(int port = 20008)
        {
            ServerSocket = new Socket(SocketType.Dgram, ProtocolType.Udp);
            ServerSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, true);

            ServerSocket.ReceiveBufferSize = SocketReceiveBufferSize;
            ServerSocket.SendBufferSize = SocketSendBufferSize;

            serverEndpoint = new IPEndPoint(IPAddress.Any, port);
            ServerSocket.Bind(serverEndpoint);
            this.port = port;
        }
        // 239.0.0.0 to 239.255.255.255
        public void SetMulticastAddress(string Ip, int port) => multicastEndpoint = new IPEndPoint(IPAddress.Parse(Ip), port);


        public void StartServer()
        {
            var e = new SocketAsyncEventArgs();
            e.Completed += RecievedFromMainSocket;
            e.SetBuffer(new byte[ClientReceiveBufferSize], 0, ClientReceiveBufferSize);
            e.RemoteEndPoint = serverEndpoint;// new IPEndPoint(IPAddress.Any, 0);

            ReceiveFromMainEp(e);
        }

        private void ReceiveFromMainEp(SocketAsyncEventArgs e)
        {
            if (!ServerSocket.ReceiveFromAsync(e))
            {
                ThreadPool.QueueUserWorkItem((cb) => RecievedFromMainSocket(null, e));
            }
        }

        // kinda like accept
        private void RecievedFromMainSocket(object sender, SocketAsyncEventArgs e)
        {
            HandleMessage(e);
            e.RemoteEndPoint = serverEndpoint;
            e.SetBuffer(0, ClientReceiveBufferSize);
            ReceiveFromMainEp(e);
        }



        private void HandleMessage(SocketAsyncEventArgs e)
        {
            var clientRemoteEndpoint = e.RemoteEndPoint as IPEndPoint;
            if (RegisteredClients.TryAdd(clientRemoteEndpoint, null))
            {
                HandleClientRegistered(e);
            }

            byte[] buffer = new byte[e.BytesTransferred];
            Buffer.BlockCopy(e.Buffer, e.Offset, buffer, 0, e.BytesTransferred + e.Offset);
            OnBytesRecieved?.Invoke(clientRemoteEndpoint, buffer);
        }

        private void HandleClientRegistered(SocketAsyncEventArgs acceptedArg)
        {
            var endpoint = acceptedArg.RemoteEndPoint as IPEndPoint;
            Console.WriteLine(" new client arrivd registering IP:" + endpoint.Address + "Port: " + endpoint.Port);
            OnClientAccepted?.Invoke(acceptedArg.AcceptSocket);
            //mux
            var e = new SocketAsyncEventArgs();
            e.Completed += RecievedFromKnownSocket;
            e.SetBuffer(new byte[ClientReceiveBufferSize], 0, ClientReceiveBufferSize);
            e.RemoteEndPoint = endpoint;

            ReceiveFromKnownClient(e);
        }

        private void ReceiveFromKnownClient(SocketAsyncEventArgs e)
        {
            if (!ServerSocket.ReceiveFromAsync(e))
            {
                ThreadPool.UnsafeQueueUserWorkItem((cb) => RecievedFromKnownSocket(null, e), null);
            }
        }

        private void RecievedFromKnownSocket(object sender, SocketAsyncEventArgs e)
        {
            HandleMessage(e);
            e.SetBuffer(0, e.Buffer.Length);
            ReceiveFromKnownClient(e);
        }

        public void SendBytesToAllClients(byte[] bytes)
        {
            //Parallel.ForEach(RegisteredClients, client =>
            //{
            //    SendBytesToClient(client.Key, bytes);
            //});
            foreach (var client in RegisteredClients)
            {
                SendBytesToClient(client.Key, bytes);
            }
        }

        public void SendBytesToClient(IPEndPoint clientEndpoint, byte[] bytes)
        {
            ServerSocket.BeginSendTo(bytes, 0, bytes.Length, SocketFlags.None, clientEndpoint, (ar) => ServerSocket.EndSendTo(ar), null);
        }

        public void MulticastMessage(byte[] message)
        {
            if (multicastEndpoint != null)
                ServerSocket.BeginSendTo(message, 0, message.Length, SocketFlags.None, multicastEndpoint, (ar) => ServerSocket.EndSendTo(ar), null);

        }



    }
}
