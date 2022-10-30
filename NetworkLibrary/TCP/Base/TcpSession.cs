using NetworkLibrary.Components;
using NetworkLibrary.Components.MessageBuffer;
using NetworkLibrary.TCP.Base;
using NetworkLibrary.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkLibrary.TCP.Base
{
   
    internal class TcpSession : IAsyncSession 
    {
        #region Fields & Props
        protected IMessageProcessQueue messageBuffer;
        // this ill be on SAEA objects and provided by buffer provider.
        protected byte[] sendBuffer;
        protected byte[] sendBuffer_;
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
        Spinlock spinlock = new Spinlock();

        internal bool UseQueue = true;
        #endregion

        private long totalBytesSend;
        private long totalBytesReceived;
      
        public TcpSession(SocketAsyncEventArgs acceptedArg, Guid sessionId, BufferProvider bufferManager)
        {
            SessionId = sessionId;
            sessionSocket = acceptedArg.AcceptSocket;
            this.bufferManager = bufferManager;
        }

        #region Initialisation
        public virtual void StartSession()
        {
            ConfigureSocket();
            InitialiseSendArgs();
            InitialiseReceiveArgs();
            messageBuffer = CreateMessageBuffer();
            Receive();
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
            sendBuffer_ = sendBuffer;
            ClientSendEventArg.SetBuffer(sendBuffer, 0, sendBuffer.Length);
        }

        protected virtual void InitialiseReceiveArgs()
        {
            ClientRecieveEventArg = new SocketAsyncEventArgs();
            ClientRecieveEventArg.Completed += BytesRecieved;

            recieveBuffer = bufferManager.GetReceiveBuffer();
            ClientRecieveEventArg.SetBuffer(recieveBuffer, 0, recieveBuffer.Length);
        }

        protected virtual IMessageProcessQueue CreateMessageBuffer()
        {
            if (UseQueue)
            {
                return new MessageQueue<PlainMessageWriter>(maxIndexedMemory, new PlainMessageWriter());
            }
            else
            {
                return new MessageBuffer(maxIndexedMemory, writeLengthPrefix: false);
            }
            //if(CoreAssemblyConfig.UseUnmanaged)
            //    return new MessageQueue<UnsafePlainMessageWriter>(maxIndexedMemory, new UnsafePlainMessageWriter());
            //else
            //    return new MessageQueue<PlainMessageWriter>(maxIndexedMemory,new PlainMessageWriter());
        }

        #endregion Initialisation

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
            Interlocked.Add(ref totalBytesReceived, e.BytesTransferred);

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
        public void SendAsync(byte[] buffer, int offset, int count)
        {
            try
            {
                spinlock.Take();
                if (SendOperationSemaphore.CurrentCount < 1 && messageBuffer.TryEnqueueMessage(buffer,offset,count))
                {
                    Interlocked.Increment(ref receiveInProgress);
                    return;
                }
            }
            finally
            {
                spinlock.Release();

            }

            // }
            if (dropOnCongestion && SendOperationSemaphore.CurrentCount < 1)
                return;

            try
            {
                SendOperationSemaphore.Wait(semaphoreCancellation.Token);
            }
            catch (Exception) { return; };

            messageBuffer.TryEnqueueMessage(buffer,offset,count);
            if (messageBuffer.TryFlushQueue(ref sendBuffer, 0, out int amountWritten))
                FlushSendBuffer(0, amountWritten);

            return;
        }

        protected void SendOrEnqueue(byte[] bytes)
        {
            //lock (exitLock)
            //{
            try
            {
                spinlock.Take();
                if (SendOperationSemaphore.CurrentCount < 1 && messageBuffer.TryEnqueueMessage(bytes))
                {
                    Interlocked.Increment(ref receiveInProgress);
                    return;
                }
            }
            finally
            {
                spinlock.Release();

            }

            // }
            if (dropOnCongestion && SendOperationSemaphore.CurrentCount < 1)
                return;

            try
            {
                SendOperationSemaphore.Wait(semaphoreCancellation.Token);
            }
            catch (Exception) { return; };

            messageBuffer.TryEnqueueMessage(bytes);
            if (messageBuffer.TryFlushQueue(ref sendBuffer, 0, out int amountWritten))
                FlushSendBuffer(0, amountWritten);

            return;

        }

        private void FlushSendBuffer(int offset, int count)
        {
            try
            {
                Interlocked.Exchange(ref sendInProgress, 1);
                Interlocked.Add(ref totalBytesSend, count);
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


            if (messageBuffer.TryFlushQueue(ref sendBuffer, 0, out int amountWritten))
            {
                FlushSendBuffer(0, amountWritten);
            }
            else
            {
                bool flushAgain = false;
                // here it means queue was empty and there was nothing to flush.
                // but this check is clearly not atomic, if during the couple cycles in between something is enqueued, 
                // i have to flush that part ,or it will stuck at queue since consumer is exiting.
                //lock (exitLock)
                //{
                try
                {
                    spinlock.Take();
                    if (!messageBuffer.IsEmpty())
                    {
                        flushAgain = true;
                    }
                    else
                    {
                        SendOperationSemaphore.Release();
                        Interlocked.Exchange(ref sendInProgress, 0);

                        return;
                    }
                }
                finally
                {
                    spinlock.Release();

                }

                // }
                if (flushAgain && messageBuffer.TryFlushQueue(ref sendBuffer, 0, out amountWritten))
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
                ReleaseSendResources();
                ReleaseReceiveResources();

            }

        }

        private void ReleaseSendResources()
        {
            if (Interlocked.CompareExchange(ref SendBufferReleased, 1, 0) == 0)
            {
                semaphoreCancellation.Cancel();
                SendOperationSemaphore.Dispose();

                DcAndDispose();
                bufferManager.ReturnSendBuffer(ref sendBuffer_);
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

        public SessionStatistics GetSessionStatistics()
        {

            return new SessionStatistics(messageBuffer.CurrentIndexedMemory,
                                         (float)((float)messageBuffer.CurrentIndexedMemory / (float)maxIndexedMemory),
                                         totalBytesSend,
                                         totalBytesReceived,
                                         messageBuffer.TotalMessageDispatched);
        }

       


        #endregion
    }
}
