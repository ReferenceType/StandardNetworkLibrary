using NetworkLibrary.Components.MessageQueue;
using NetworkLibrary.TCP.Base.Interface;
using NetworkLibrary.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkLibrary.TCP.Base
{
    internal class TcpSession : IAsyncSession
    {
        protected IMessageQueue messageQueue;
        // this ill be on SAEA objects and provided by buffer provider.
        protected byte[] sendBuffer;
        protected byte[] recieveBuffer;

        protected SocketAsyncEventArgs ClientSendEventArg;
        protected SocketAsyncEventArgs ClientRecieveEventArg;

        protected Socket sessionSocket;

        public event Action<Guid, byte[], int, int> OnBytesRecieved;
        public event Action<Guid> OnSessionClosed;

        public Guid SessionId;

        internal int socketRecieveBufferSize = 128000;
        internal int socketSendBufferSize = 128000;
        internal int maxIndexedMemory = 1280000;
        protected int currentIndexedMemory = 0;
        internal bool dropOnCongestion = false;


        private int disposeStatus = 0;
        private int sendInProgress = 0;
        private int receiveInProgress = 0;
        protected int SessionClosing = 0;
        private int SendBufferReleased = 0;
        private int ReceiveBufferReleased = 0;

        private BufferProvider bufferManager;

        private SemaphoreSlim SendOperationSemaphore;
        private int disconnectStatus;
        protected readonly object exitLock = new object();
        CancellationTokenSource semaphoreCancellation = new CancellationTokenSource();

        public TcpSession(SocketAsyncEventArgs acceptedArg, Guid sessionId, BufferProvider bufferManager)
        {
            SessionId = sessionId;
            sessionSocket = acceptedArg.AcceptSocket;
            this.bufferManager = bufferManager;
        }

        protected virtual void ConfigureSocket()
        {
            sessionSocket.ReceiveBufferSize = socketRecieveBufferSize;
            sessionSocket.SendBufferSize = socketSendBufferSize;
        }

        protected virtual void InitialiseSendArgs()
        {
            ClientSendEventArg = new SocketAsyncEventArgs();
            ClientSendEventArg.Completed += Sent;

            SendOperationSemaphore = new SemaphoreSlim(1, 1);
            sendBuffer = bufferManager.GetSendBuffer();

            ClientSendEventArg.SetBuffer(sendBuffer, 0, sendBuffer.Length);
        }

        protected virtual void InitialiseReceiveArgs()
        {
            ClientRecieveEventArg = new SocketAsyncEventArgs();
            ClientRecieveEventArg.Completed += BytesRecieved;

            recieveBuffer = bufferManager.GetReceiveBuffer();
            ClientRecieveEventArg.SetBuffer(recieveBuffer, 0, recieveBuffer.Length);
        }

        protected virtual void ConfigureMessageQueue()
        {
            messageQueue = new MessageQueue(maxIndexedMemory);
        }

        public virtual void StartSession()
        {
            ConfigureSocket();
            InitialiseSendArgs();
            InitialiseReceiveArgs();
            ConfigureMessageQueue();
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
                ThreadPool.UnsafeQueueUserWorkItem((e) => BytesRecieved(null, ClientRecieveEventArg), null);
            }
        }


        private void BytesRecieved(object sender, SocketAsyncEventArgs e)
        {
            if (IsSessionClosing())
            {
                ReleaseReceiveResources();
                return;
            }
            Interlocked.Decrement(ref receiveInProgress);

            if (e.SocketError != SocketError.Success)
            {
                HandleError(e, "while recieving from ");
                Disconnect();
                return;
            }
            else if (e.BytesTransferred == 0)
            {
                HandleError(e, " 0 bytes recieved");
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
            OnBytesRecieved?.Invoke(SessionId, buffer, offset, count);
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
                if (SendOperationSemaphore.CurrentCount < 1 && messageQueue.TryEnqueueMessage(bytes))
                {
                    return;
                }
            }

            if (dropOnCongestion && SendOperationSemaphore.CurrentCount < 1)
                return;

            try
            {
                SendOperationSemaphore.Wait(semaphoreCancellation.Token);
            }
            catch (Exception) { return; };

            SendBytesAsync(bytes, 0, bytes.Length);
            return;

        }

        private void SendBytesAsync(byte[] bytes, int offset, int count)
        {
            messageQueue.TryEnqueueMessage(bytes);
            if (messageQueue.TryFlushQueue(ref sendBuffer, 0, out int amountWritten))
                FlushSendBuffer(0, amountWritten);

        }
        private void FlushSendBuffer(int offset, int count)
        {
            try
            {
                Interlocked.CompareExchange(ref sendInProgress, 1, 0);
                ClientSendEventArg.SetBuffer(sendBuffer, offset, count);
                if (!sessionSocket.SendAsync(ClientSendEventArg))
                {
                    ThreadPool.UnsafeQueueUserWorkItem((e) => Sent(null, ClientSendEventArg), null);
                }
            }
            catch (ObjectDisposedException) { Interlocked.CompareExchange(ref sendInProgress, 0, 1); }
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
                HandleError(e, "While sending the client ");
                return;
            }

            else if (e.BytesTransferred < e.Count)
            {
                e.SetBuffer(e.Offset + e.BytesTransferred, e.Count - e.BytesTransferred);
                if (!sessionSocket.SendAsync(e))
                {
                    Task.Run(() => Sent(null, e));
                }
                return;
            }

            //int count = 0;

            if (messageQueue.TryFlushQueue(ref sendBuffer, 0, out int amountWritten))
            {
                FlushSendBuffer(0, amountWritten);
            }
            else
            {
                bool flushAgain = false;
                // here it means queue was empty and there was nothing to flush.
                // but this check is clearly not atomic, if during the couple cycles in between something is enqueued, 
                // i have to flush that part ,or it will stuck at queue since consumer is exiting.
                lock (exitLock)
                {
                    if (!messageQueue.IsEmpty())
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
                if (flushAgain && messageQueue.TryFlushQueue(ref sendBuffer, 0, out amountWritten))
                {
                    FlushSendBuffer(0, amountWritten);
                }
            }
        }

        #endregion Send


        // TODO
        protected void Disconnect()
        {
            EndSession();
        }

        // TODO 
        protected void HandleError(SocketAsyncEventArgs e, string context)
        {
            MiniLogger.Log(MiniLogger.LogLevel.Error, context + Enum.GetName(typeof(SocketError), e.SocketError));
        }

        private bool IsSessionClosing()
        {
            return Interlocked.CompareExchange(ref SessionClosing, 1, 1) == 1;
        }

        public virtual void EndSession()
        {
            // is it the first time im being called?
            if (Interlocked.CompareExchange(ref SessionClosing, 1, 0) == 0)
            {
                try
                {
                    sessionSocket.Shutdown(SocketShutdown.Both);
                }
                catch (Exception) { }

                // is send operation not in progress? release now otherise will be released once operation completes.
                if (Interlocked.CompareExchange(ref sendInProgress, 1, 1) == 0)
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
                semaphoreCancellation.Cancel();
                SendOperationSemaphore.Dispose();

                DcAndDispose();
                bufferManager.ReturnSendBuffer(ref sendBuffer);
            }

        }

        private void ReleaseReceiveResources()
        {
            if (Interlocked.CompareExchange(ref ReceiveBufferReleased, 1, 0) == 0)
            {
                DcAndDispose();
                bufferManager.ReturnReceiveBuffer(ref recieveBuffer);
            }

        }

        protected void DcAndDispose()
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
            MiniLogger.Log(MiniLogger.LogLevel.Debug, string.Format("Session with Guid: {0} is disposed", SessionId));
            GC.SuppressFinalize(this);

        }

        public void Dispose()
        {
            Dispose(disposing: true);
        }


        #endregion
    }
}
