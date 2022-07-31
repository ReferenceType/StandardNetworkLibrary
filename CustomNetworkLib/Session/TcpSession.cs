using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CustomNetworkLib
{
    public class TcpSession : IAsyncSession
    {
        protected byte[] sendBuffer = new byte[12000000];
        protected byte[] recieveBuffer = new byte[12800000];

        protected SocketAsyncEventArgs ClientSendEventArg;
        protected SocketAsyncEventArgs ClientRecieveEventArg;
        protected SocketAsyncEventArgs sessionEventArg;

        protected ConcurrentQueue<byte[]> SendQueue = new ConcurrentQueue<byte[]>();

        public event EventHandler<byte[]> OnBytesRecieved;
        public Guid SessionId;

        private int maxMem=100000000;
        private int currMem=0;

        public TcpSession(SocketAsyncEventArgs acceptedArg, Guid sessionId)
        {
            sendBuffer = new byte[12800000];
            ConfigureSendArgs(acceptedArg);
            ConfigureRecieveArgs(acceptedArg);

            StartRecieving();
        }

        protected virtual void ConfigureSendArgs(SocketAsyncEventArgs acceptedArg)
        {
            this.sessionEventArg = acceptedArg;
            var sendArg = new SocketAsyncEventArgs();
            sendArg.Completed += Sent;

            var token = new UserToken(acceptedArg.AcceptSocket);
            token.Guid = Guid.NewGuid();
            sendArg.UserToken = token;
            sendArg.AcceptSocket = acceptedArg.AcceptSocket;
            sendArg.SetBuffer(sendBuffer, 0, sendBuffer.Length);
            ClientSendEventArg = sendArg;
            
        }

        protected virtual void ConfigureRecieveArgs(SocketAsyncEventArgs acceptedArg)
        {
            var recieveArg = new SocketAsyncEventArgs();
            recieveArg.Completed += BytesRecieved;

            var token = new UserToken(acceptedArg.AcceptSocket);
            token.Guid = Guid.NewGuid();
            recieveArg.UserToken = new UserToken(acceptedArg.AcceptSocket);
            recieveArg.AcceptSocket = acceptedArg.AcceptSocket;
            ClientRecieveEventArg = recieveArg;
            ClientRecieveEventArg.SetBuffer(recieveBuffer, 0, recieveBuffer.Length);
        }

        protected virtual void StartRecieving()
        {
            sessionEventArg.AcceptSocket.ReceiveAsync(ClientRecieveEventArg);
        }

        protected virtual void BytesRecieved(object sender, SocketAsyncEventArgs e)
        {
            
            if (e.SocketError != SocketError.Success)
            {
                HandleError(e, "while recieving header from ");
                return;
            }
            else if (e.BytesTransferred == 0)
            {
                DisconnectClient(e);
                return;
            }

            HandleRecieveComplete(e.Buffer,e.Offset,e.BytesTransferred);

            e.SetBuffer(0, e.Buffer.Length);
            if (!sessionEventArg.AcceptSocket.ReceiveAsync(e))
            {
                BytesRecieved(null, e);
            }
        }

        protected virtual void HandleRecieveComplete(byte[] buffer,int offset,int count)
        {
            byte[] message = new byte[count+offset];
            Buffer.BlockCopy(buffer, 0, message, 0, count+offset);
            OnBytesRecieved?.Invoke(null,message);
        }
      
        // TODO
        protected void DisconnectClient(SocketAsyncEventArgs e)
        {
        }

        public void SendAsync(byte[] bytes)
        {
            SendOrEnqueue(bytes);
        }

       
        protected virtual void SendOrEnqueue(byte[] bytes)
        {
            var token = (UserToken)ClientSendEventArg.UserToken;
            if (Volatile.Read(ref currMem) < maxMem &&token.OperationSemaphore.CurrentCount < 1)
            {
                
                Interlocked.Add(ref currMem,bytes.Length);
                SendQueue.Enqueue(bytes);
                return;
            }

            token.OperationSemaphore.Wait();
            Send(bytes);
        }

        protected virtual void Send(byte[] bytes) 
        {
            Buffer.BlockCopy(bytes, 0, sendBuffer, 0, bytes.Length);
            ClientSendEventArg.SetBuffer(0, bytes.Length);
            if (!sessionEventArg.AcceptSocket.SendAsync(ClientSendEventArg))
            {
                Sent(null, ClientSendEventArg);
            }
        }
        protected void Sent(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                HandleError(e, "while sending the client");
               
                return;
            }

            else if (e.BytesTransferred<e.Count)
            {
                var token = (UserToken)e.UserToken;
                // isnt it minus
                e.SetBuffer(e.Offset + e.BytesTransferred, e.Count - e.BytesTransferred);
                if (!token.ClientSocket.SendAsync(e))
                {
                    Sent(null, e);
                }
                return;
            }
            
            if (SendQueue.TryDequeue(out var bytes))
            {
                if (currMem > 100096000)
                    throw new Exception();
                Interlocked.Add(ref currMem, -bytes.Length);

                Send(bytes);
            }
            else
            {
                ((UserToken)e.UserToken).OperationCompleted();

            }

        }

        // TODO
        protected void HandleError(SocketAsyncEventArgs e, string v)
        {
            Console.WriteLine(v);
        }
    }
}
