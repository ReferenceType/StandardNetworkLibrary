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
        protected byte[] sendBuffer;
        protected byte[] recieveBuffer;

        protected SocketAsyncEventArgs ClientSendEventArg;
        protected SocketAsyncEventArgs ClientRecieveEventArg;
        protected Socket sessionSocket;
        protected ConcurrentQueue<byte[]> SendQueue = new ConcurrentQueue<byte[]>();

        public virtual event EventHandler<byte[]> OnBytesRecieved;
        public Guid SessionId;

        private int maxMem=1000000000;
        private int currMem=0;

        public TcpSession(SocketAsyncEventArgs acceptedArg, Guid sessionId)
        {

            sessionSocket = acceptedArg.AcceptSocket;
            ConfigureSendArgs(acceptedArg);
            ConfigureRecieveArgs(acceptedArg);

            StartRecieving();
            //ThreadPool.SetMinThreads(2000, 2000);
            
        }

        protected virtual void ConfigureSendArgs(SocketAsyncEventArgs acceptedArg)
        {
            var sendArg = new SocketAsyncEventArgs();
            sendArg.Completed += Sent;

            var token = new UserToken();
            token.Guid = Guid.NewGuid();

            sendArg.UserToken = token;
            sendBuffer = new byte[12800000];

            sendArg.SetBuffer(sendBuffer, 0, sendBuffer.Length);
            ClientSendEventArg = sendArg;
            
        }

        protected virtual void ConfigureRecieveArgs(SocketAsyncEventArgs acceptedArg)
        {
            var recieveArg = new SocketAsyncEventArgs();
            recieveArg.Completed += BytesRecieved;

            var token = new UserToken();
            token.Guid = Guid.NewGuid();
            
            recieveArg.UserToken = new UserToken();
            recieveBuffer = new byte[12800000];

            ClientRecieveEventArg = recieveArg;
            ClientRecieveEventArg.SetBuffer(recieveBuffer, 0, recieveBuffer.Length);
        }

        protected virtual void StartRecieving()
        {
            sessionSocket.ReceiveAsync(ClientRecieveEventArg);
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
            if (!sessionSocket.ReceiveAsync(e))
            {
                BytesRecieved(null, e);
            }
        }

        protected virtual void HandleRecieveComplete(byte[] buffer,int offset,int count)
        {
            byte[] message = new byte[count];
            Buffer.BlockCopy(buffer, offset, message, 0, count);
            OnBytesRecieved?.Invoke(null, message);
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
            SendBuffer(bytes,0,bytes.Length);

            //List<ArraySegment<byte>> segments =  new List<ArraySegment<byte>>();
            //segments.Add(new ArraySegment<byte>(bytes));
            //ClientSendEventArg.BufferList= segments;
        }

        protected virtual void SendBuffer(byte[] bytes, int offset,int count)
        {
            //List<ArraySegment<byte>> segments = new List<ArraySegment<byte>>();
            //segments.Add(new ArraySegment<byte>(bytes));
            //ClientSendEventArg.SetBuffer(null,0,0);
            //ClientSendEventArg.BufferList = segments;

            ClientSendEventArg.SetBuffer(offset, count);
            if (!sessionSocket.SendAsync(ClientSendEventArg))
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
                e.SetBuffer(e.Offset + e.BytesTransferred, e.Count - e.BytesTransferred);
                if (!sessionSocket.SendAsync(e))
                {
                    Sent(null, e);
                }
                return;
            }
            
            if (SendQueue.TryDequeue(out var bytes))
            {
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
