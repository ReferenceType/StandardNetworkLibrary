using NetworkLibrary.Components;
using NetworkLibrary.Components.MessageBuffer;
using NetworkLibrary.Components.Statistics;
using NetworkLibrary.Utils;
using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;

namespace NetworkLibrary.TCP.Base
{
    internal class TcpSession : IAsyncSession
    {
        #region Fields & Props

        public event Action<Guid, byte[], int, int> OnBytesRecieved;
        public event Action<Guid> OnSessionClosed;
        public Guid SessionId;
        public int SocketRecieveBufferSize = 128000;
        public int MaxIndexedMemory = 128000000;
        public bool DropOnCongestion = false;

        protected IMessageQueue messageBuffer;
        protected byte[] sendBuffer;
        protected byte[] recieveBuffer;
        protected SocketAsyncEventArgs ClientSendEventArg;
        protected SocketAsyncEventArgs ClientRecieveEventArg;
        protected Socket sessionSocket;
        protected int currentIndexedMemory = 0;
        protected int SessionClosing = 0;
        protected Spinlock SendSemaphore = new Spinlock();
        protected Spinlock enqueueLock = new Spinlock();

        internal int socketSendBufferSize = 128000;
        protected internal bool UseQueue = true;

        private int disposeStatus = 0;
        private int SendBufferReleased = 0;
        private int ReceiveBufferReleased = 0;
        private int disconnectStatus;
        private long totalBytesSend;
        private long totalBytesReceived;
        private long totalBytesSendPrev = 0;
        private long totalBytesReceivedPrev = 0;
        private long totalMessageReceived = 0;
        private long totalMsgReceivedPrev;
        private long totalMSgSentPrev;
//#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
//        Memory<byte> receiveMemory;
//#endif
        #endregion

        public IPEndPoint RemoteEndpoint => (IPEndPoint)sessionSocket.RemoteEndPoint;

        public TcpSession(SocketAsyncEventArgs acceptedArg, Guid sessionId)
        {
            SessionId = sessionId;
            sessionSocket = acceptedArg.AcceptSocket;
        }
        public TcpSession(Socket sessionSockt, Guid sessionId)
        {
            SessionId = sessionId;
            sessionSocket = sessionSockt;
        }

        #region Initialisation
        public virtual void StartSession()
        {
            ConfigureSocket();
            ConfigureBuffers();
            InitialiseSendArgs();
            InitialiseReceiveArgs();
            messageBuffer = CreateMessageQueue();
            ThreadPool.UnsafeQueueUserWorkItem((e) => Receive(),null);
        }

        protected virtual void ConfigureSocket()
        {
            sessionSocket.ReceiveBufferSize = SocketRecieveBufferSize;
            sessionSocket.SendBufferSize = socketSendBufferSize;
        }
        protected virtual void ConfigureBuffers()
        {
            recieveBuffer = BufferPool.RentBuffer(socketSendBufferSize);
//#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
//            receiveMemory= new Memory<byte>(recieveBuffer);
//#endif
            if (UseQueue) sendBuffer = BufferPool.RentBuffer(SocketRecieveBufferSize);

        }
        protected virtual void InitialiseSendArgs()
        {
            ClientSendEventArg = new SocketAsyncEventArgs();
            ClientSendEventArg.Completed += SendComplete;
            if (UseQueue) ClientSendEventArg.SetBuffer(sendBuffer, 0, sendBuffer.Length);

            SendSemaphore = new Spinlock();
        }

        protected virtual void InitialiseReceiveArgs()
        {
            ClientRecieveEventArg = new SocketAsyncEventArgs();
            ClientRecieveEventArg.Completed += BytesRecieved;

            ClientRecieveEventArg.SetBuffer(recieveBuffer, 0, recieveBuffer.Length);
        }

        protected virtual IMessageQueue CreateMessageQueue()
        {
            if (UseQueue)
            {
                return new MessageQueue<MessageWriter>(MaxIndexedMemory, new MessageWriter());
            }
            else
            {
                return new MessageBuffer(MaxIndexedMemory, writeLengthPrefix: false);
            }

        }

        #endregion Initialisation

        #region Recieve 
        protected virtual void Receive()
        {
//#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
//            ReceiveModern();
//            return;
//#endif
            if (IsSessionClosing())
            {
                ReleaseReceiveResourcesIdempotent();
                return;
            }
            try
            {
                ClientRecieveEventArg.SetBuffer(0, ClientRecieveEventArg.Buffer.Length);
                if (!sessionSocket.ReceiveAsync(ClientRecieveEventArg))
                {
                    ThreadPool.UnsafeQueueUserWorkItem((e) => BytesRecieved(null, ClientRecieveEventArg), null);
                }
            }
            catch (Exception ex)
            when (ex is ObjectDisposedException || ex is NullReferenceException)
            { ReleaseReceiveResourcesIdempotent(); }



        }
//#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER

//        private async void ReceiveModern()
//        {
//            try
//            {
//                while (true)
//                {
//                    if (IsSessionClosing())
//                    {
//                        ReleaseReceiveResourcesIdempotent();
//                        return;
//                    }

//                    int amountReceived = await sessionSocket.ReceiveAsync(receiveMemory,
//                        SocketFlags.None).ConfigureAwait(false);

//                    if (IsSessionClosing())
//                    {
//                        ReleaseReceiveResourcesIdempotent();
//                        return;
//                    }
//                    if (amountReceived == 0)
//                    {
//                        Disconnect();
//                        ReleaseReceiveResourcesIdempotent();
//                        return;
//                    }
//                    totalBytesReceived += amountReceived;
//                    HandleReceived(recieveBuffer, 0, amountReceived);
//                }
//            }
//            catch (Exception e)
//            {
//                if (!IsSessionClosing())
//                {
//                    MiniLogger.Log(MiniLogger.LogLevel.Error,
//                        "While receiving on tcp session error occured;" +e.Message);
//                    EndSession();
//                }
//            }
//        }
//#endif

        private void BytesRecieved(object sender, SocketAsyncEventArgs e)
        {
            if (IsSessionClosing())
            {
                ReleaseReceiveResourcesIdempotent();
                return;
            }

            if (e.SocketError != SocketError.Success)
            {
                HandleError(e, "while recieving from ");
                Disconnect();
                ReleaseReceiveResourcesIdempotent();
                return;
            }
            else if (e.BytesTransferred == 0)
            {
                Disconnect();
                ReleaseReceiveResourcesIdempotent();
                return;
            }
            totalBytesReceived += e.BytesTransferred;

            HandleReceived(e.Buffer, e.Offset, e.BytesTransferred);
            Receive();

        }

        protected virtual void HandleReceived(byte[] buffer, int offset, int count)
        {
            totalMessageReceived++;
            OnBytesRecieved?.Invoke(SessionId, buffer, offset, count);
        }

        #endregion Recieve

        #region Send
        public virtual void SendAsync(byte[] bytes)
        {
            if (IsSessionClosing())
                return;
            try
            {
                SendOrEnqueue(bytes);
            }
            catch (Exception e)
            {
                if (!IsSessionClosing())
                    MiniLogger.Log(MiniLogger.LogLevel.Error,
                        "Unexcpected error while sending async with tcp session" + e.Message + "Trace " + e.StackTrace);
            }
        }

        public void SendAsync(byte[] buffer, int offset, int count)
        {
            if (IsSessionClosing())
                return;
            try
            {
                SendOrEnqueue(buffer, offset, count);
            }
            catch (Exception e)
            {
                if (!IsSessionClosing())
                    MiniLogger.Log(MiniLogger.LogLevel.Error,
                        "Unexcpected error while sending async with tcp session" + e.Message + "Trace " + e.StackTrace);
            }

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SendOrEnqueue(byte[] buffer, int offset, int count)
        {
            enqueueLock.Take();
            if (IsSessionClosing())
                ReleaseSendResourcesIdempotent();
            if (SendSemaphore.IsTaken() && messageBuffer.TryEnqueueMessage(buffer, offset, count))
            {
                enqueueLock.Release();

                return;
            }
            enqueueLock.Release();

            if (DropOnCongestion && SendSemaphore.IsTaken())
                return;


            SendSemaphore.Take();
            if (IsSessionClosing())
            {
                ReleaseSendResourcesIdempotent();
                SendSemaphore.Release();
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
            if (IsSessionClosing())
                ReleaseSendResourcesIdempotent();
            if (SendSemaphore.IsTaken() && messageBuffer.TryEnqueueMessage(bytes))
            {
                enqueueLock.Release();
                return;
            }
            enqueueLock.Release();

            if (DropOnCongestion && SendSemaphore.IsTaken()) return;

            SendSemaphore.Take();
            if (IsSessionClosing())
            {
                ReleaseSendResourcesIdempotent();
                SendSemaphore.Release();
                return;
            }

            messageBuffer.TryEnqueueMessage(bytes);
            messageBuffer.TryFlushQueue(ref sendBuffer, 0, out int amountWritten);
            FlushSendBuffer(0, amountWritten);

            return;

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void FlushSendBuffer(int offset, int count)
        {
//#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            //ThreadPool.UnsafeQueueUserWorkItem((e) => SendModern(offset,count),null);
            //return;
//#endif
            try
            {
                totalBytesSend += count;
                ClientSendEventArg?.SetBuffer(sendBuffer, offset, count);
                if (!sessionSocket.SendAsync(ClientSendEventArg))
                {
                    ThreadPool.UnsafeQueueUserWorkItem((e) => SendComplete(null, ClientSendEventArg), null);

                }
            }
            catch { EndSession(); ReleaseSendResourcesIdempotent(); }
        }
//#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
//        private async void SendModern(int offset, int count)
//        {
//            try
//            {


//            Top:
//                await sessionSocket.SendAsync(new ReadOnlyMemory<byte>(sendBuffer, offset, count),
//                    SocketFlags.None).ConfigureAwait(false);

//                if (IsSessionClosing())
//                {
//                    SendSemaphore.Release();
//                    ReleaseSendResourcesIdempotent();
//                    return;
//                }

//                if (messageBuffer.TryFlushQueue(ref sendBuffer, 0, out int amountWritten))
//                {
//                    //FlushSendBuffer(0, amountWritten);
//                    offset = 0;
//                    count = amountWritten;
//                    goto Top;
//                }
//                else
//                {
//                    bool flushAgain = false;
//                    // here it means queue was empty and there was nothing to flush.
//                    // but this check is clearly not atomic, if during the couple cycles in between something is enqueued, 
//                    // i have to flush that part ,or it will stuck at queue since consumer is exiting.

//                    enqueueLock.Take();
//                    if (!messageBuffer.IsEmpty())
//                    {
//                        flushAgain = true;
//                        enqueueLock.Release();
//                    }
//                    else
//                    {
//                        messageBuffer.Flush();

//                        SendSemaphore.Release();
//                        enqueueLock.Release();
//                        if (IsSessionClosing())
//                        {
//                            ReleaseSendResourcesIdempotent();
//                        }
//                        return;
//                    }

//                    if (flushAgain && messageBuffer.TryFlushQueue(ref sendBuffer, 0, out amountWritten))
//                    {
//                        // FlushSendBuffer(0, amountWritten);
//                        offset = 0;
//                        count = amountWritten;
//                        goto Top;
//                    }
//                }
//            }
//                        catch { EndSession(); ReleaseSendResourcesIdempotent(); }

//        }
//#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SendComplete(object ignored, SocketAsyncEventArgs e)
        {
            try
            {
                if (IsSessionClosing())
                {
                    SendSemaphore.Release();
                    ReleaseSendResourcesIdempotent();
                    return;
                }

                if (e.SocketError != SocketError.Success)
                {
                    SendSemaphore.Release();
                    HandleError(e, "While sending the client ");
                    ReleaseSendResourcesIdempotent();
                    return;
                }
                else if (e.BytesTransferred < e.Count)
                {
                    e.SetBuffer(e.Offset + e.BytesTransferred, e.Count - e.BytesTransferred);
                    if (!sessionSocket.SendAsync(e))
                    {
                        ThreadPool.UnsafeQueueUserWorkItem((ee) => SendComplete(null, e), null);
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

                        SendSemaphore.Release();
                        enqueueLock.Release();
                        if (IsSessionClosing())
                        {
                            ReleaseSendResourcesIdempotent();
                        }
                        return;
                    }

                    if (flushAgain && messageBuffer.TryFlushQueue(ref sendBuffer, 0, out amountWritten))
                    {
                        FlushSendBuffer(0, amountWritten);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!IsSessionClosing())
                {
                    MiniLogger.Log(MiniLogger.LogLevel.Error,"Error on sent callback tcp session"+ ex.Message);
                    EndSession();
                }
            }

        }

        #endregion Send

        protected void Disconnect()
        {
            EndSession();
        }

        protected void HandleError(SocketAsyncEventArgs e, string context)
        {
            MiniLogger.Log(MiniLogger.LogLevel.Error, context + Enum.GetName(typeof(SocketError), e.SocketError));
            EndSession();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool IsSessionClosing()
        {
            return Interlocked.CompareExchange(ref SessionClosing, 1, 1) == 1;
        }

        public virtual void EndSession()
        {
            // is it the first time im being called?
            if (Interlocked.CompareExchange(ref SessionClosing, 1, 0) == 0)
            {
                //SendSemaphore.Release();
                try
                {
                    sessionSocket.Shutdown(SocketShutdown.Both);
                }
                catch (Exception) { }
                try
                {
                    DcAndDispose();
                }
                catch { }
            }

        }

        protected virtual void ReleaseSendResourcesIdempotent()
        {
            if (Interlocked.CompareExchange(ref SendBufferReleased, 1, 0) == 0)
            {
                ReleaseSendResources();
            }

        }
        private void ReleaseSendResources()
        {
            try
            {
                ClientSendEventArg.Dispose();
                Interlocked.Exchange(ref messageBuffer, null)?.Dispose();

                if (UseQueue)
                    BufferPool.ReturnBuffer(sendBuffer);
            }
            catch (Exception e)
            {
                MiniLogger.Log(MiniLogger.LogLevel.Error,
                "Error eccured while releasing tcp session send resources:" + e.Message);
            }
            finally
            {
                enqueueLock.Release();
            }

        }
        private void ReleaseReceiveResourcesIdempotent()
        {
            if (Interlocked.CompareExchange(ref ReceiveBufferReleased, 1, 0) == 0)
            {
                ReleaseReceiveResources();
            }

        }

        protected virtual void ReleaseReceiveResources()
        {
            try
            {
                ClientRecieveEventArg.Dispose();
                ClientRecieveEventArg = null;
                BufferPool.ReturnBuffer(recieveBuffer);
            }
            catch { }
          
        }

        protected void DcAndDispose()
        {
            // can be calles only once
            if (Interlocked.CompareExchange(ref disconnectStatus, 1, 0) == 1)
                return;

            SocketAsyncEventArgs e = new SocketAsyncEventArgs();
            e.Completed += OnDisconnected;
            e.DisconnectReuseSocket = false;

            if (!sessionSocket.DisconnectAsync(e))
                OnDisconnected(null, e);
        }
        private void OnDisconnected(object ignored, SocketAsyncEventArgs e)
        {
            OnSessionClosed?.Invoke(SessionId);
            OnSessionClosed = null;
            Dispose();
        }

        public SessionStatistics GetSessionStatistics()
        {
            var deltaReceived = totalBytesReceived - totalBytesReceivedPrev;
            var deltaSent = totalBytesSend - totalBytesSendPrev;
            totalBytesSendPrev = totalBytesSend;
            totalBytesReceivedPrev = totalBytesReceived;

            long deltaMSgReceived = totalMessageReceived - totalMsgReceivedPrev;
            long deltaMsgSent = messageBuffer.TotalMessageDispatched - totalMSgSentPrev;

            totalMsgReceivedPrev = totalMessageReceived;
            totalMSgSentPrev = messageBuffer.TotalMessageDispatched;

            return new SessionStatistics(messageBuffer.CurrentIndexedMemory,
                                         (float)((float)messageBuffer.CurrentIndexedMemory / (float)MaxIndexedMemory),
                                         totalBytesReceived,
                                         totalBytesSend,
                                         deltaReceived,
                                         deltaSent,
                                         messageBuffer.TotalMessageDispatched,
                                         totalMessageReceived,
                                         deltaMsgSent,
                                         deltaMSgReceived);
        }
        #region Dispose
        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref disposeStatus, 1, 0) == 1)
            {
                return;
            }

            OnBytesRecieved = null;
            OnSessionClosed = null;

            sessionSocket.Close();
            sessionSocket.Dispose();
            sessionSocket = null;
            if (!SendSemaphore.IsTaken())
            {
                ReleaseSendResourcesIdempotent();
            }
            SendSemaphore.Release();
            MiniLogger.Log(MiniLogger.LogLevel.Debug, string.Format("Session with Guid: {0} is disposed", SessionId));

        }

        public void Dispose()
        {
            Dispose(disposing: true);
        }

        #endregion
    }
}
