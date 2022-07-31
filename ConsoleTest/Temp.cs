//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.Net;
//using System.Net.Sockets;
//using System.Text;
//using CustomNetworkLib;

//namespace NetworkSystem
//{
//    public class AsyncUdpServer
//    {
//        public delegate void ClientAccepted(Socket ClientSocket);
//        public delegate void BytesRecieved(int id, byte[] bytes);
//        public ClientAccepted OnClientAccepted;
//        public BytesRecieved OnBytesRecieved;

//        protected Socket ServerSocket;
//        protected ConcurrentDictionary<int, SocketAsyncEventArgs> ClientSendEventArgs = new ConcurrentDictionary<int, SocketAsyncEventArgs>();
//        protected ConcurrentDictionary<IPEndPoint, SocketAsyncEventArgs> Registar = new ConcurrentDictionary<IPEndPoint, SocketAsyncEventArgs>();
//        protected int totalNumClient = 0;
//        protected int port = 0;
//        private SocketAsyncEventArgs maınSocket;

//        public AsyncUdpServer(int port = 20008)
//        {
//            ServerSocket = new Socket(SocketType.Dgram, ProtocolType.Udp);
//            ServerSocket.Bind(new IPEndPoint(IPAddress.Any, port));
//            this.port = port;
//            maınSocket = new SocketAsyncEventArgs();
//            maınSocket.Completed += RecievedFromMainSocket;
//            maınSocket.SetBuffer(new byte[64000], 0, 64000);
//            maınSocket.RemoteEndPoint = new IPEndPoint(IPAddress.Any, port);

//            RecieveFromMainSocket(maınSocket);
//        }

//        private void StartRecieving()
//        {
//            SocketAsyncEventArgs e1 = new SocketAsyncEventArgs();
//            e1.Completed += RecievedFromMainSocket;
//            e1.SetBuffer(new byte[64000], 0, 64000);
//            e1.RemoteEndPoint = new IPEndPoint(IPAddress.Any, port);
//            RecieveFromMainSocket(e1);
//        }

//        private void RecieveFromMainSocket(SocketAsyncEventArgs e)
//        {
//            Console.WriteLine("Recieving");
//            if (!ServerSocket.ReceiveMessageFromAsync(e))
//            {
//                RecievedFromMainSocket(null, e);
//            }
//        }
//        // kinda like accept
//        private void RecievedFromMainSocket(object sender, SocketAsyncEventArgs e)
//        {
//            //if (IsExistingClient(e))
//            //{
//            //  StartRecieving();

//            //}

//            //else
//            //{
//            //    e.Completed -= RecievedFromMainSocket;
//            //    e.Completed += RecievedFromRegisteredClient;

//            //    RecieveFromMainSocket(e);
//            //}

//            //    RecieveFromMainSocket(e);

//            //VerifyClient(e);
//            if (e.SocketError != SocketError.Success)
//            {
//                Console.WriteLine(Enum.GetName(typeof(SocketError), e.SocketError));
//                return;
//            }

//            byte[] buffer = new byte[e.BytesTransferred];
//            Buffer.BlockCopy(e.Buffer, e.Offset, buffer, 0, e.BytesTransferred + e.Offset);
//            OnBytesRecieved?.Invoke(0, buffer);
//            // RecieveFromMainSocket(e);
//            StartRecieving();
//        }

//        private void RecievedFromRegisteredClient(object sender, SocketAsyncEventArgs e)
//        {
//            if (e.SocketError != SocketError.Success)
//            {
//                Console.WriteLine(Enum.GetName(typeof(SocketError), e.SocketError));
//                return;
//            }

//            byte[] buffer = new byte[e.BytesTransferred];
//            Buffer.BlockCopy(e.Buffer, e.Offset, buffer, 0, e.BytesTransferred + e.Offset);
//            OnBytesRecieved?.Invoke(0, buffer);
//            RecieveFromMainSocket(e);

//        }

//        private bool IsExistingClient(SocketAsyncEventArgs e)
//        {
//            if (Registar.TryAdd(((IPEndPoint)e.RemoteEndPoint), e))
//            {
//                RegisterClient(e);
//                return false;
//            }
//            return true;
//        }

//        private void RegisterClient(SocketAsyncEventArgs e)
//        {

//            Console.WriteLine(" new client arrivd registering");

//            //// Send args
//            var sendArg = new SocketAsyncEventArgs();
//            sendArg.Completed += Sent;
//            sendArg.UserToken = new UserToken(e.AcceptSocket);
//            sendArg.AcceptSocket = e.AcceptSocket;
//            sendArg.SetBuffer(new byte[64000], 0, 64000);
//            sendArg.RemoteEndPoint = e.RemoteEndPoint;

//            ClientSendEventArgs.TryAdd(((IPEndPoint)e.RemoteEndPoint).Port, sendArg);
//            HandleClientAccepted(e);

//        }
//        private void HandleClientAccepted(SocketAsyncEventArgs acceptedArg)
//        {
//            OnClientAccepted?.Invoke(acceptedArg.AcceptSocket);
//        }

//        private void Sent(object sender, SocketAsyncEventArgs e)
//        {
//            var token = e.UserToken as UserToken;
//            token.OperationPending.Set();
//        }

//        public void SendBytesToClient(int id, byte[] bytes)
//        {
//            var token = ClientSendEventArgs[id].UserToken as UserToken;
//            token.OperationPending.WaitOne();

//            ClientSendEventArgs[id].SetBuffer(bytes, 0, bytes.Length);
//            if (!ServerSocket.SendToAsync(ClientSendEventArgs[id]))
//            {
//                Sent(null, ClientSendEventArgs[id]);
//            }
//        }
//        public void SendBytesToAllClients(byte[] bytes)
//        {
//            foreach (var item in ClientSendEventArgs)
//            {
//                SendBytesToClient(item.Key, bytes);
//            }
//        }




//    }
//}
