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

        public event Action<byte[], int, int> OnBytesRecieved;
        public event Action<Guid> OnSessionClosed;

        public Guid SessionId;
        protected int prefixLenght = 0;

        internal int SAEASendBufferSize = 128000;
        internal int recieveBufferSize = 128000;
        internal int MaxIndexedMemory = 1280000;
        protected int currentIndexedMemory = 0;
        internal bool DropOnCongestion = false;

        internal bool fragmentedMessageExist = false;
        internal bool isPrefxWritten;
        internal int fragmentedMsgOffset = 0;
        internal byte[] fragmentedMsg;

        private int disposeStatus = 0;
        private int sendInProgress = 0;
        private int receiveInProgress = 0;
        private int SessionClosing = 0;
        private int SendBufferReleased = 0;
        private int ReceiveBufferReleased = 0;

        private SemaphoreSlim SendOperationSemaphore;
        private int disconnectStatus;
        protected readonly object exitLock = new object();
        CancellationTokenSource semaphoreCancellation =  new CancellationTokenSource();

        public TcpSession(SocketAsyncEventArgs acceptedArg, Guid sessionId)
        {
            SessionId = sessionId;
            sessionSocket = acceptedArg.AcceptSocket;

            ConfigureSocket();
            ConfigureSendArgs(acceptedArg);
            ConfigureRecieveArgs(acceptedArg);
        }

        protected virtual void ConfigureSocket()
        {
            sessionSocket.ReceiveBufferSize = 128000;
            sessionSocket.SendBufferSize = 128000;
        }

        protected virtual void ConfigureSendArgs(SocketAsyncEventArgs acceptedArg)
        {
            var sendArg = new SocketAsyncEventArgs();
            sendArg.Completed += Sent;

            SendOperationSemaphore = new SemaphoreSlim(1,1);
            sendBuffer = BufferManager.GetSendBuffer();

            sendArg.SetBuffer(sendBuffer, 0, sendBuffer.Length);
            ClientSendEventArg = sendArg;
        }

        protected virtual void ConfigureRecieveArgs(SocketAsyncEventArgs acceptedArg)
        {
            var recieveArg = new SocketAsyncEventArgs();
            recieveArg.Completed += BytesRecieved;

            recieveBuffer = BufferManager.GetReceiveBuffer();

            ClientRecieveEventArg = recieveArg;
            ClientRecieveEventArg.SetBuffer(recieveBuffer, 0, recieveBuffer.Length);
        }
        public void StartSession()
        {
            Receive();

        }
        #region Recieve 
        protected virtual void Receive()
        {
            Interlocked.Increment(ref receiveInProgress);
            if (IsSessionClosing())
            {
                ReleaseReceiveResources();
                return;
            }

            ClientRecieveEventArg.SetBuffer(0, ClientRecieveEventArg.Buffer.Length);
            if (!sessionSocket.ReceiveAsync(ClientRecieveEventArg))
            {
                ThreadPool.UnsafeQueueUserWorkItem((e) => BytesRecieved(null, ClientRecieveEventArg),null);
            }
        }


        protected virtual void BytesRecieved(object sender, SocketAsyncEventArgs e)
        {
            Interlocked.Decrement(ref receiveInProgress);
            if (IsSessionClosing())
            {
                ReleaseReceiveResources();
                return;
            }
            if (e.SocketError != SocketError.Success)
            {
                HandleError(e, "while recieving from ");
                Disconnect();
                return;
            }
            else if (e.BytesTransferred == 0)
            {
                Console.WriteLine(" 0 bytes recieved");
                Disconnect();
                return;
            }

            HandleRecieveComplete(e.Buffer, e.Offset, e.BytesTransferred);
            // here            
            try
            {
                Receive();
            }
            catch (ObjectDisposedException) { }

        }

        protected virtual void HandleRecieveComplete(byte[] buffer, int offset, int count)
        {
            OnBytesRecieved?.Invoke(buffer, offset, count);
        }

        #endregion Recieve

        #region Send
        public virtual void SendAsync(byte[] bytes)
        {
            if (IsSessionClosing())
                return;
            SendOrEnqueue(bytes);
        }


        protected virtual void SendOrEnqueue(byte[] bytes)
        {
            lock (exitLock)
            {
                if (Volatile.Read(ref currentIndexedMemory) < MaxIndexedMemory && SendOperationSemaphore.CurrentCount < 1)
                {
                    Interlocked.Add(ref currentIndexedMemory, bytes.Length + prefixLenght);
                    SendQueue.Enqueue(bytes);
                    return;
                }
  
            }

            if (DropOnCongestion && SendOperationSemaphore.CurrentCount < 1)
                return;

            try
            {
                SendOperationSemaphore.Wait(semaphoreCancellation.Token);
            }
            catch (ObjectDisposedException) { return; };

            Interlocked.Add(ref currentIndexedMemory, bytes.Length + prefixLenght);
            SendQueue.Enqueue(bytes);
            ConsumeQueueAndSend();
            return;

        }

        private bool ConsumeQueueAndSend()
        {
            
            //if (IsSessionClosing())
            //{
            //    ReleaseSendResources();
            //    return false;
            //}
            bool flushedSomething = FlushQueue(ref sendBuffer, 0, out int offset);
            if (flushedSomething)
            {
                try
                {
                    Interlocked.CompareExchange(ref sendInProgress, 1, 0);
                    ClientSendEventArg.SetBuffer(sendBuffer, 0, offset);
                    if (!sessionSocket.SendAsync(ClientSendEventArg))
                    {
                        ThreadPool.UnsafeQueueUserWorkItem((e) => Sent(null, ClientSendEventArg), null);
                    }
                }
                catch (ObjectDisposedException) { return false; }
              
            }

            return flushedSomething;
        }
        private bool FlushQueue(ref byte[] sendBuffer, int offset, out int count)
        {
            count = 0;
            CheckLeftoverMessage(ref sendBuffer, ref offset, ref count);
            if (count == sendBuffer.Length)
                return true;
            
            while (SendQueue.TryDequeue(out var bytes))
            {
                // buffer cant carry entire message not enough space
                if (prefixLenght + bytes.Length > sendBuffer.Length - offset)
                {
                    fragmentedMsg = bytes;
                    fragmentedMessageExist = true;

                    int available = sendBuffer.Length - offset;
                    if (available < prefixLenght)
                        break;

                    Interlocked.Add(ref currentIndexedMemory, -(available));
                    WriteMessagePrefix(ref sendBuffer, offset, bytes.Length);
                    isPrefxWritten = true;

                    offset += prefixLenght;
                    available -= prefixLenght;

                    Buffer.BlockCopy(bytes, fragmentedMsgOffset, sendBuffer, offset, available);
                    count = sendBuffer.Length;

                    fragmentedMsgOffset += available;
                    break;
                }

                Interlocked.Add(ref currentIndexedMemory, -(prefixLenght + bytes.Length));

                WriteMessagePrefix(ref sendBuffer, offset, bytes.Length);
                offset += prefixLenght;

                Buffer.BlockCopy(bytes, 0, sendBuffer, offset, bytes.Length);
                offset += bytes.Length;
                count += prefixLenght + bytes.Length;
            }
            return count != 0;

        }

        private void CheckLeftoverMessage(ref byte[] sendBuffer, ref int offset, ref int count)
        {
            if (fragmentedMessageExist)
            {
                int available = sendBuffer.Length - offset;
                if (!isPrefxWritten)
                {
                    isPrefxWritten = true;
                    WriteMessagePrefix(ref sendBuffer, offset, fragmentedMsg.Length);
                    offset += prefixLenght;
                    available -= prefixLenght;
                }
                // will fit
                if (fragmentedMsg.Length - fragmentedMsgOffset <= available)
                {
                    Interlocked.Add(ref currentIndexedMemory, -(fragmentedMsg.Length - fragmentedMsgOffset));
                    Buffer.BlockCopy(fragmentedMsg, fragmentedMsgOffset, sendBuffer, offset, fragmentedMsg.Length - fragmentedMsgOffset);

                    offset += fragmentedMsg.Length - fragmentedMsgOffset;
                    count += offset;

                    // reset
                    fragmentedMsgOffset = 0;
                    fragmentedMsg = null;
                    isPrefxWritten = false;
                    fragmentedMessageExist = false;
                }
                // wont fit read until you fill buffer
                else
                {
                    Interlocked.Add(ref currentIndexedMemory, -(available));
                    Buffer.BlockCopy(fragmentedMsg, fragmentedMsgOffset, sendBuffer, offset, available);

                    count = sendBuffer.Length;
                    fragmentedMsgOffset += available;
                }
            }
        }

        protected virtual void Sent(object ignored, SocketAsyncEventArgs e)
        {
            if (IsSessionClosing())
            {
                ReleaseSendResources();
                return;
            }

            if (e.SocketError != SocketError.Success)
            {
                HandleError(e, "while sending the client");
                return;
            }

            else if (e.BytesTransferred < e.Count)
            {
                e.SetBuffer(e.Offset + e.BytesTransferred, e.Count - e.BytesTransferred);
                if (!sessionSocket.SendAsync(e))
                {
                    Task.Run(()=>Sent(null, e));
                }
                return;
            }

            if (!ConsumeQueueAndSend())
            {
                bool flushAgain = false;
                // here it means queue was empty and there was nothing to flush.
                // but this check is not atomic, if during the couple cycles in between something is enqueued, 
                // i have to flush that part ,or it will stuck at queue since consumer is exiting.
                lock (exitLock)
                {
                    if (SendQueue.Count > 0)
                    {
                        flushAgain = true;
                    }
                    else
                    {
                        SendOperationSemaphore.Release();
                        Interlocked.CompareExchange(ref sendInProgress, 0, 1);
                        return;
                    }
                }
                if (flushAgain)
                    ConsumeQueueAndSend();
            }
        }

        #endregion Send


        protected virtual void WriteMessagePrefix(ref byte[] buffer,int offset,int messageLength) {}

        // TODO
        protected void Disconnect()
        {
            EndSession();
        }

        // TODO 
        protected void HandleError(SocketAsyncEventArgs e, string v)
        {
            Console.WriteLine(v+Enum.GetName(typeof(SocketError),e.SocketError));
        }

        private bool IsSessionClosing()
        {
            return Interlocked.CompareExchange(ref SessionClosing, 1, 1) == 1;
        }

        public void EndSession()
        {
            semaphoreCancellation.Cancel();
            // is it the first time im being called?
            if (Interlocked.CompareExchange(ref SessionClosing, 1, 0)==0)
            {
                try
                {
                    sessionSocket.Shutdown(SocketShutdown.Both);
                }
                catch (Exception) { }
              
                // is send operation not in progress? release now otherise will be released once operation completes.
                if(Interlocked.CompareExchange(ref sendInProgress, 1, 1) == 0)
                {
                    ReleaseSendResources();
                }
                // is recieve operation not in progress? release now otherise will be released once operation completes.
                if (Interlocked.CompareExchange(ref receiveInProgress, 1, 1) == 0)
                {
                    ReleaseReceiveResources();
                }
               
            }
          
        }

        private void ReleaseSendResources()
        {
            if (Interlocked.CompareExchange(ref SendBufferReleased, 1, 0) == 0)
            {
                SendOperationSemaphore.Dispose();
                DcAndDispose();
                BufferManager.ReturnSendBuffer(ref sendBuffer);
            }
               
        }

        private void ReleaseReceiveResources()
        {
            if (Interlocked.CompareExchange(ref ReceiveBufferReleased, 1, 0) == 0)
            {
                DcAndDispose();
                BufferManager.ReturnReceiveBuffer(ref recieveBuffer);
            }
              
        }

        private void DcAndDispose()
        {
            // can be calles only once
            if (Interlocked.CompareExchange(ref disconnectStatus, 1, 0) == 1)
                return;

            SocketAsyncEventArgs e = new SocketAsyncEventArgs();
            e.Completed += OnDisconnected;
            e.DisconnectReuseSocket = false;
            
            sessionSocket.DisconnectAsync(e);
        }
        private void OnDisconnected(object ignored, SocketAsyncEventArgs e)
        {
            OnSessionClosed?.Invoke(SessionId);
            Dispose();
        }

        #region Dispose
        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref disposeStatus, 1, 0) == 1)
            {
                return;
            }
            sessionSocket.Close();
            ClientSendEventArg.Dispose();
            ClientRecieveEventArg.Dispose();

          

            sessionSocket.Dispose();
            Console.WriteLine("disposed");
            GC.SuppressFinalize(this);

        }

        public void Dispose()
        {
            Dispose(disposing: true);
        }

        
        #endregion
    }
}
