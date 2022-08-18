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
    public class Pair<T,U> where T:class where U:class
    {
        public T Item1;
        public U Item2;
        public Pair(T item1, U item2)
        {
            Item1 = item1;
            Item2 = item2;
        }
        public void Release()
        {
            Item1 = null;
            Item2 = null;
        }
    }
    public class TcpSession : IAsyncSession
    {
        protected byte[] sendBuffer;
        protected byte[] recieveBuffer;

        protected SocketAsyncEventArgs ClientSendEventArg;
        protected SocketAsyncEventArgs ClientRecieveEventArg;
        protected Socket sessionSocket;
        protected ConcurrentQueue<Pair<byte[], byte[]>> SendQueue = new ConcurrentQueue<Pair<byte[], byte[]>>();

        public virtual event EventHandler<byte[]> OnBytesRecieved;
        public Guid SessionId;
        private int sendBufferSize = 128000;
        private int recieveBufferSize = 128000;
        private int maxMem=128000;
        private int currMem=0;
        int offset=0;
        int count=0;
        private readonly object locker = new object();


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
            sendBuffer = new byte[sendBufferSize];

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
            recieveBuffer = new byte[recieveBufferSize];

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
            SendOrEnqueue(ref bytes);
        }

       
        protected virtual void SendOrEnqueue(ref byte[] bytes)
        {
            var token = (UserToken)ClientSendEventArg.UserToken;
            if (Volatile.Read(ref currMem) < maxMem &&token.OperationSemaphore.CurrentCount < 1)
            {
                
                Interlocked.Add(ref currMem,bytes.Length);
                byte[] byteFrame = BitConverter.GetBytes(bytes.Length);
                Interlocked.Add(ref currMem, byteFrame.Length);
                
                //SendQueue.Enqueue(byteFrame);
                //SendQueue.Enqueue(bytes);
                
                SendQueue.Enqueue(new Pair<byte[], byte[]>(byteFrame, bytes));
                return;
            }

            //token.OperationSemaphore.Wait();
            token.WaitOperationCompletion();
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

            //ClientSendEventArg.SetBuffer(offset, count);
            ClientSendEventArg.SetBuffer(sendBuffer, 0, count);
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
             if (sendBuffer.Length > sendBufferSize)
            {
                sendBuffer = new byte[sendBufferSize];
                e.SetBuffer(sendBuffer, 0, sendBufferSize);
            }
            offset = 0;
            while(SendQueue.TryPeek(out var bytes))
            {
                
                if(offset==0 && bytes.Item1.Length + bytes.Item2.Length > sendBuffer.Length)
                {
                    sendBuffer=new byte[bytes.Item1.Length + bytes.Item2.Length];
                    e.SetBuffer(sendBuffer,0,sendBuffer.Length);
                    //GC.Collect();

                }

                if (bytes.Item1.Length + bytes.Item2.Length > sendBuffer.Length - offset)
                    break;

                SendQueue.TryDequeue(out bytes);
                Interlocked.Add(ref currMem, -(bytes.Item1.Length+bytes.Item2.Length));

                Buffer.BlockCopy(bytes.Item1, 0, sendBuffer, offset, bytes.Item1.Length);
                offset += bytes.Item1.Length;
                Buffer.BlockCopy(bytes.Item2, 0, sendBuffer, offset, bytes.Item2.Length);

                offset+=bytes.Item2.Length;
                bytes.Release();

               
            }
            if (offset != 0)
            {
               // GC.Collect();
                e.SetBuffer(sendBuffer, 0, offset);
                //e.SetBuffer( 0, offset);
                if (!sessionSocket.SendAsync(ClientSendEventArg))
                {
                    Sent(null, ClientSendEventArg);
                }
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
