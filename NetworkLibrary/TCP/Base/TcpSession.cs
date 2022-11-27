using NetworkLibrary.Components;
using NetworkLibrary.Components.MessageBuffer;
using NetworkLibrary.Components.Statistics;
using NetworkLibrary.TCP.Base;
using NetworkLibrary.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
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
        protected int SessionClosing = 0;
        private int SendBufferReleased = 0;
        private int ReceiveBufferReleased = 0;


        private Spinlock SendOperationLock= new Spinlock();
        private Spinlock enqueueLock = new Spinlock();

        private int disconnectStatus;


        internal bool UseQueue = true;
        #endregion

        private long totalBytesSend;
        private long totalBytesReceived;

        public IPEndPoint RemoteEndpoint => (IPEndPoint)sessionSocket.RemoteEndPoint;

        public TcpSession(SocketAsyncEventArgs acceptedArg, Guid sessionId)
        {
            SessionId = sessionId;
            sessionSocket = acceptedArg.AcceptSocket;
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
            ClientSendEventArg.Completed += SendComplete;

            SendOperationLock = new Spinlock();
            sendBuffer = BufferPool.RentBuffer(socketRecieveBufferSize);
            sendBuffer_ = sendBuffer;
            ClientSendEventArg.SetBuffer(sendBuffer, 0, sendBuffer.Length);
        }

        protected virtual void InitialiseReceiveArgs()
        {
            ClientRecieveEventArg = new SocketAsyncEventArgs();
            ClientRecieveEventArg.Completed += BytesRecieved;

            recieveBuffer = BufferPool.RentBuffer(socketSendBufferSize);
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
          
        }

        #endregion Initialisation

        #region Recieve 
        protected virtual void Receive()
        {
            if (IsSessionClosing())
            {
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
                Disconnect();
                return;
            }
            Interlocked.Add(ref totalBytesReceived, e.BytesTransferred);

            HandleRecieveComplete(e.Buffer, e.Offset, e.BytesTransferred);
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
            if (IsSessionClosing())
                return;
            SendOrEnqueue(buffer, offset, count);


        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SendOrEnqueue(byte[] buffer, int offset, int count)
        {
            enqueueLock.Take();
            if (SendOperationLock.IsTaken() && messageBuffer.TryEnqueueMessage(buffer, offset, count))
            {
                enqueueLock.Release();

                return;
            }
            enqueueLock.Release();

            if (dropOnCongestion && SendOperationLock.IsTaken())
                return;

            
            SendOperationLock.Take();
            if (IsSessionClosing())
            {
                SendOperationLock.Release();
                return;
            }

            messageBuffer.TryEnqueueMessage(buffer, offset, count);

            messageBuffer.TryFlushQueue(ref sendBuffer, 0, out int amountWritten);
            FlushSendBuffer(0, amountWritten);

            return;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SendOrEnqueue(byte[] bytes)
        {
            enqueueLock.Take();
            if (SendOperationLock.IsTaken() && messageBuffer.TryEnqueueMessage(bytes))
            {
                enqueueLock.Release();
                return;
            }
            enqueueLock.Release();

            if (dropOnCongestion && SendOperationLock.IsTaken()) return;

            SendOperationLock.Take();
            if (IsSessionClosing())
            {
                SendOperationLock.Release(); 
                return;
            }

            messageBuffer.TryEnqueueMessage(bytes);
            messageBuffer.TryFlushQueue(ref sendBuffer, 0, out int amountWritten);
            FlushSendBuffer(0, amountWritten);

            return;

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FlushSendBuffer(int offset, int count)
        {
            try
            {
                Interlocked.Add(ref totalBytesSend, count);
                ClientSendEventArg.SetBuffer(sendBuffer, offset, count);
                if (!sessionSocket.SendAsync(ClientSendEventArg))
                {
                    ThreadPool.UnsafeQueueUserWorkItem((e) => SendComplete(null, ClientSendEventArg), null);

                }
            }
            catch (ObjectDisposedException) { }
        }


        private void SendComplete(object ignored, SocketAsyncEventArgs e)
        {
            if (IsSessionClosing())
            {
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
                    ThreadPool.UnsafeQueueUserWorkItem((ee) => SendComplete(null, e),null);
                    MiniLogger.Log(MiniLogger.LogLevel.Info, "Resending");
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
               
                enqueueLock.Take();
                if (!messageBuffer.IsEmpty())
                {
                    flushAgain = true;
                    enqueueLock.Release();
                }
                else
                {
                    messageBuffer.Flush();
                    SendOperationLock.Release();
                    enqueueLock.Release();
                    return;
                }

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
                SendOperationLock.Release();
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
               // SendOperationSemaphore.Dispose();

                DcAndDispose();
                BufferPool.ReturnBuffer(sendBuffer_);
            }

        }

        private void ReleaseReceiveResources()
        {
            if (Interlocked.CompareExchange(ref ReceiveBufferReleased, 1, 0) == 0)
            {
                DcAndDispose();
                BufferPool.ReturnBuffer(recieveBuffer);
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

        public SessionStatistics GetSessionStatistics()
        {

            return new SessionStatistics(messageBuffer.CurrentIndexedMemory,
                                         (float)((float)messageBuffer.CurrentIndexedMemory / (float)maxIndexedMemory),
                                         totalBytesSend,
                                         totalBytesReceived,
                                         messageBuffer.TotalMessageDispatched);
        }
        #region Dispose
        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref disposeStatus, 1, 0) == 1)
            {
                return;
            }
            messageBuffer?.Dispose();
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
