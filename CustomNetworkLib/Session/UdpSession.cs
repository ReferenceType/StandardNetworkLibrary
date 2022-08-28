using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace CustomNetworkLib.Session.Interface
{
    internal class UdpSession : IAsyncSession
    {
        public event Action<byte[],int,int> OnBytesRecieved;

        protected SocketAsyncEventArgs clientSocketSendArgs;
        protected SocketAsyncEventArgs RecieveEventArg;
        protected EndPoint remoteEndPoint;
        protected Socket sessionSocket;

        protected  byte[] recieveBuffer = new byte[1000000];
        public UdpSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            remoteEndPoint = e.RemoteEndPoint;
            sessionSocket = e.AcceptSocket;

            // e.AcceptSocket
            RecieveEventArg = new SocketAsyncEventArgs();
            RecieveEventArg.Completed += Recieved;
            RecieveEventArg.SetBuffer(new byte[64000], 0, 64000);
            RecieveEventArg.UserToken = new UserToken();
            RecieveEventArg.AcceptSocket = e.AcceptSocket;
            RecieveEventArg.RemoteEndPoint = remoteEndPoint;


            //e.Completed -= Connected;
            //e.Completed += Recieved;
            //e.SetBuffer(new byte[64000], 0, 64000);
            //e.UserToken = new UserToken(e.ConnectSocket);
            //e.AcceptSocket = e.ConnectSocket;

            clientSocketSendArgs = new SocketAsyncEventArgs();
            clientSocketSendArgs.SetBuffer(new byte[64000], 0, 64000);
            clientSocketSendArgs.UserToken = new UserToken();
            clientSocketSendArgs.AcceptSocket = e.AcceptSocket;
            clientSocketSendArgs.RemoteEndPoint = remoteEndPoint;
            clientSocketSendArgs.Completed += Sent;

            // conncted = true;
            //if (!sessionSocket.ReceiveFromAsync(RecieveEventArg))
            //{
            //    Recieved(null, e);
            //}

            sessionSocket.BeginReceiveFrom(recieveBuffer, 0, recieveBuffer.Length, SocketFlags.None, ref remoteEndPoint, EndrecieveFrom, null);
        }

        private void EndrecieveFrom(IAsyncResult ar)
        {
            int amount = sessionSocket.EndReceiveFrom(ar, ref remoteEndPoint);

            byte[] buffer = new byte[amount];
            Buffer.BlockCopy(recieveBuffer, 0, buffer, 0, amount);

            OnBytesRecieved?.Invoke(recieveBuffer,0,amount);
            sessionSocket.BeginReceiveFrom(recieveBuffer, 0, recieveBuffer.Length, SocketFlags.None, ref remoteEndPoint, EndrecieveFrom, null);
        }
    

    

        private void Recieve(SocketAsyncEventArgs e)
        {
           
            if (!sessionSocket.ReceiveMessageFromAsync(e))
            {
                Recieved(null, e);
            }
        }
        private void Recieved(object p, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                Console.WriteLine(Enum.GetName(typeof(SocketError), e.SocketError));
                return;
            }
            //byte[] buffer = new byte[e.BytesTransferred];
            //Buffer.BlockCopy(e.Buffer, e.Offset, buffer, 0, e.BytesTransferred + e.Offset);

            e.SetBuffer(0, e.Buffer.Length);

            OnBytesRecieved?.Invoke(e.Buffer, e.Offset,e.BytesTransferred);
            e.RemoteEndPoint = remoteEndPoint;
            Recieve(e);
        }

        public void SendAsync(byte[] buffer)
        {
            SendBytes(buffer);
        }

        public void SendBytes(byte[] bytes)
        {
            //var token = clientSocketSendArgs.UserToken as UserToken;
            //token.OperationPending.WaitOne();

            //Console.WriteLine("sending" + ((IPEndPoint)clientSocketSendArgs.RemoteEndPoint).Port);
            //clientSocketSendArgs.RemoteEndPoint = remoteEndPoint;
            //clientSocketSendArgs.SetBuffer(bytes, 0, bytes.Length);

            //sessionSocket.SendTo(bytes, remoteEndPoint);

            //sessionSocket.BeginSendTo(bytes, 0, bytes.Length, SocketFlags.None, remoteEndPoint, EndSendCallback, null);
            sessionSocket.BeginSendTo(bytes, 0, bytes.Length, SocketFlags.None, remoteEndPoint, (IAsyncResult ar) => sessionSocket.EndSend(ar), null);

            //if (!sessionSocket.SendToAsync(clientSocketSendArgs))
            //{
            //    Sent(null, clientSocketSendArgs);
            //}
        }

        private void EndSendCallback(IAsyncResult ar)
        {
            sessionSocket.EndSend(ar);
        }

        private void Sent(object sender, SocketAsyncEventArgs e)
        {
            //Console.Write("Sent");
            var token = clientSocketSendArgs.UserToken as UserToken;
            token.OperationPending.Set();
        }
       
        }

        
    }


