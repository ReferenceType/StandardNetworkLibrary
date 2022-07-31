using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using CustomNetworkLib;

namespace NetworkSystem
{
    public class AsyncUdpServer
    {
        public delegate void ClientAccepted(Socket ClientSocket);
        public delegate void BytesRecieved(int id, byte[] bytes);
        public ClientAccepted OnClientAccepted;
        public BytesRecieved OnBytesRecieved;

        protected Socket ServerSocket;
        protected ConcurrentDictionary<IPEndPoint, SocketAsyncEventArgs> ClientSendEventArgs = new ConcurrentDictionary<IPEndPoint, SocketAsyncEventArgs>();
        protected ConcurrentDictionary<IPEndPoint, SocketAsyncEventArgs> Registar = new ConcurrentDictionary<IPEndPoint, SocketAsyncEventArgs>();
        protected int totalNumClient = 0;
        protected int port = 0;
        protected IPEndPoint recieveEndpoint = new IPEndPoint(IPAddress.Any, 0);

        public AsyncUdpServer(int port = 20008)
        {
            ServerSocket = new Socket(SocketType.Dgram, ProtocolType.Udp);
            //ServerSocket.Blocking = false;
            //ServerSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            // Apply the option: exclusive address use
            ServerSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, true);
            // Apply the option: dual mode (this option must be applied before recieving/sending)
            if (ServerSocket.AddressFamily == AddressFamily.InterNetworkV6)
                ServerSocket.DualMode = true;

           
            ServerSocket.Bind(new IPEndPoint(IPAddress.Any, port));


            this.port = port;
            var e = new SocketAsyncEventArgs();
            e.Completed += RecievedFromMainSocket;
            e.SetBuffer(new byte[64000], 0, 64000);
            e.RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            
            RecieveFromMainSocket(e);
        }

        private void StartRecieving()
        {
            SocketAsyncEventArgs e1 = new SocketAsyncEventArgs();
            e1.Completed += RecievedFromMainSocket;
            e1.SetBuffer(new byte[64000], 0, 64000);
            e1.RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            RecieveFromMainSocket(e1);
        }

        private void RecieveFromMainSocket(SocketAsyncEventArgs e)
        {
           
            if (!ServerSocket.ReceiveFromAsync(e))
            {
                RecievedFromMainSocket(null, e);
            }
        }
        // kinda like accept
        private void RecievedFromMainSocket(object sender, SocketAsyncEventArgs e)
        {
            //if (IsExistingClient(e))
            //{
            //  StartRecieving();

            //}

            //else
            //{
            //    e.Completed -= RecievedFromMainSocket;
            //    e.Completed += RecievedFromRegisteredClient;

            //    RecieveFromMainSocket(e);
            //}

            //    RecieveFromMainSocket(e);
            VerifyClient(e);
           

            //if (e.SocketError != SocketError.Success)
            //{
            //    Console.WriteLine(Enum.GetName(typeof(SocketError), e.SocketError));
            //    return;
            //}

            byte[] buffer = new byte[e.BytesTransferred];
            Buffer.BlockCopy(e.Buffer, e.Offset, buffer, 0, e.BytesTransferred + e.Offset);
            OnBytesRecieved?.Invoke(0, buffer);

            // StartRecieving();

            
            e.RemoteEndPoint = recieveEndpoint;
            e.SetBuffer(0,64000); 
            RecieveFromMainSocket(e);


        }

        private void RecievedFromRegisteredClient(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                Console.WriteLine(Enum.GetName(typeof(SocketError), e.SocketError));
                return;
            }

            byte[] buffer = new byte[e.BytesTransferred];
            Buffer.BlockCopy(e.Buffer, e.Offset, buffer, 0, e.BytesTransferred + e.Offset);
            OnBytesRecieved?.Invoke(0, buffer);

            if (!ServerSocket.ReceiveMessageFromAsync(e))
            {
                RecievedFromRegisteredClient(null, e);
            }

        }

        private bool VerifyClient(SocketAsyncEventArgs e)
        {
            if (Registar.TryAdd(((IPEndPoint)e.RemoteEndPoint), e))
            {
                RegisterClient(e);
                return false;
            }
            return true;
        }

        private void RegisterClient(SocketAsyncEventArgs e)
        {
            
            Console.WriteLine(" new client arrivd registering");

            // Send args
            var sendArg = new SocketAsyncEventArgs();
            sendArg.Completed += Sent;
            sendArg.UserToken = new UserToken(e.AcceptSocket);
            sendArg.AcceptSocket = e.AcceptSocket;
            sendArg.SetBuffer(new byte[64000], 0, 64000);
            sendArg.RemoteEndPoint = e.RemoteEndPoint;
            
            ClientSendEventArgs.TryAdd(((IPEndPoint)e.RemoteEndPoint), sendArg);
            HandleClientAccepted(e);

            //// Send args
            var recdArg = new SocketAsyncEventArgs();
            recdArg.Completed += RecievedFromRegisteredClient;
            recdArg.UserToken = new UserToken(e.AcceptSocket);
            recdArg.AcceptSocket = e.AcceptSocket;
            recdArg.SetBuffer(new byte[64000], 0, 64000);
            recdArg.RemoteEndPoint = e.RemoteEndPoint;
            //RecieveFromMainSocket(e);

            //if (!ServerSocket.ReceiveMessageFromAsync(recdArg))
            //{
            //    RecievedFromRegisteredClient(null, e);
            //}

        }

     

        private void HandleClientAccepted(SocketAsyncEventArgs acceptedArg)
        {
            OnClientAccepted?.Invoke(acceptedArg.AcceptSocket);
        }

        private void Sent(object sender, SocketAsyncEventArgs e)
        {
            var token = e.UserToken as UserToken;
            token.OperationPending.Set();
        }

        public void SendBytesToClient(IPEndPoint id, byte[] bytes)
        {
            //var token = ClientSendEventArgs[id].UserToken as UserToken;
            //token.OperationPending.WaitOne();

            //ClientSendEventArgs[id].SetBuffer(bytes, 0, bytes.Length);
           // ClientSendEventArgs[id].RemoteEndPoint = id;
            //Console.WriteLine("sw sending "+((IPEndPoint)ClientSendEventArgs[id].RemoteEndPoint).Port);
            //ServerSocket.SendTo(bytes, id);

            ServerSocket.BeginSendTo(bytes, 0, bytes.Length, SocketFlags.None, id, (IAsyncResult ar) => ServerSocket.EndSend(ar), null);
            //if (!ServerSocket.SendToAsync(ClientSendEventArgs[id]))
            //{
            //    Sent(null, ClientSendEventArgs[id]);
            //}
        }
        public void SendBytesToAllClients(byte[] bytes)
        {
            foreach (var item in ClientSendEventArgs)
            {
                SendBytesToClient(item.Key,bytes);
            }
        }
        
        

       
    }
}
