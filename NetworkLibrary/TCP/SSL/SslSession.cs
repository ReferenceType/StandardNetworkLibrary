using NetworkLibrary.Components;
using NetworkLibrary.Components.MessageBuffer;
using NetworkLibrary.Components.Statistics;
using NetworkLibrary.TCP.Base;
using NetworkLibrary.Utils;
using System;
using System.Net;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Threading;

namespace NetworkLibrary.TCP.SSL.Base
{
    public class SslSession : IAsyncSession
    {
        public event Action<Guid, byte[], int, int> OnBytesRecieved;
        public event Action<Guid> OnSessionClosed;
        public IPEndPoint RemoteEndpoint { get => RemoteEP; set => RemoteEP = value; }
        public bool DropOnCongestion { get; internal set; }
        public int MaxIndexedMemory = 128000000;
        public int SendBufferSize = 128000;
        public int ReceiveBufferSize = 128000;

        protected IMessageQueue messageQueue;
        protected Spinlock SendSemaphore = new Spinlock();
        protected Spinlock enqueueLock = new Spinlock();
        protected SslStream sessionStream;
        protected byte[] receiveBuffer;
//#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
//        protected Memory<byte> receiveMemory;
//#endif
        protected byte[] sendBuffer;
        protected Guid sessionId;
        protected IPEndPoint RemoteEP;

        protected internal bool UseQueue = false;

        private int disposedValue;
        private int SessionClosing = 0;
        private long totalBytesSend;
        private long totalBytesReceived;
        private long totalMessageReceived = 0;
        private long totalBytesSendPrev = 0;
        private long totalBytesReceivedPrev = 0;
        private long totalMsgReceivedPrev;
        private long totalMessageSentPrev;


        private int started = 0;
        public SslSession(Guid sessionId, SslStream sessionStream)
        {
            this.sessionId = sessionId;
            this.sessionStream = sessionStream;
        }
        public void StartSession()
        {
            if(Interlocked.Exchange(ref started, 1) == 0)
            {
                ConfigureBuffers();
                messageQueue = CreateMessageQueue();
                ThreadPool.UnsafeQueueUserWorkItem((s)=> Receive(),null);
            }
        }

        protected virtual void ConfigureBuffers()
        {
            receiveBuffer = /*new byte[ReceiveBufferSize];*/ BufferPool.RentBuffer(ReceiveBufferSize);

//#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER

//            receiveMemory = new Memory<byte>(receiveBuffer);
//#endif

            if (UseQueue) sendBuffer = BufferPool.RentBuffer(SendBufferSize);

        }

        protected virtual IMessageQueue CreateMessageQueue()
        {
            if (UseQueue)
                return new MessageQueue<MessageWriter>(MaxIndexedMemory, new MessageWriter());
            else
                return new MessageBuffer(MaxIndexedMemory, writeLengthPrefix: false);

        }

        public void SendAsync(byte[] buffer, int offset, int count)
        {
            if (IsSessionClosing())
                return;
            try
            {
                SendAsync_(buffer, offset, count);
            }
            catch(Exception e)
            {
                if (!IsSessionClosing())
                    MiniLogger.Log(MiniLogger.LogLevel.Error, 
                        "Unexpected error while sending async with ssl session" + e.Message+"Trace " +e.StackTrace);
            }
        }
        private void SendAsync_(byte[] buffer, int offset, int count)
        {
            enqueueLock.Take();
            if (IsSessionClosing())
            {
                ReleaseSendResourcesIdempotent();
                return;
            }
            if (SendSemaphore.IsTaken())
            {
                if (messageQueue.TryEnqueueMessage(buffer, offset, count))
                {
                    enqueueLock.Release();
                    return;
                }

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

            // you have to push it to queue because queue also does the processing.
            if(!messageQueue.TryEnqueueMessage(buffer, offset, count))
            {
                MiniLogger.Log(MiniLogger.LogLevel.Error, "Message is too large to fit on buffer");
                EndSession();
                return;
            }
            FlushAndSend();
        }
        public void SendAsync(byte[] buffer)
        {
            if (IsSessionClosing())
                return;
            try
            {
                SendAsync_(buffer);
            }
            catch (Exception e)
            {
                if (!IsSessionClosing())
                    MiniLogger.Log(MiniLogger.LogLevel.Error,
                        "Unexpected error while sending async with ssl session" + e.Message + "Trace " + e.StackTrace);
            }
        }
        private void SendAsync_(byte[] buffer)
        {
            enqueueLock.Take();
            if (IsSessionClosing())
            {
                ReleaseSendResourcesIdempotent();
                return;
            }
            if (SendSemaphore.IsTaken())
            {
                if (messageQueue.TryEnqueueMessage(buffer))
                {
                    enqueueLock.Release();
                    return;
                }
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
            if (!messageQueue.TryEnqueueMessage(buffer))
            {
                MiniLogger.Log(MiniLogger.LogLevel.Error, "Message is too large to fit on buffer");
                EndSession();
                return;
            }
            FlushAndSend();
        }

        // this can only be called inside send lock critical section
        protected void FlushAndSend()
        {
            //ThreadPool.UnsafeQueueUserWorkItem((s) => 
            //{
                try
                {
                    messageQueue.TryFlushQueue(ref sendBuffer, 0, out int amountWritten);
                    WriteOnSessionStream(amountWritten);
                }
                catch
                {
                    if (!IsSessionClosing())
                        throw;
                }

           // }, null);
           
        }
        protected void WriteOnSessionStream(int count)
        {
//#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
//            WriteModern(count);
//            return;
//#endif

            try
            {
                sessionStream.BeginWrite(sendBuffer, 0, count, SentInternal, null);
            }
            catch (Exception ex)
            {
                HandleError("While attempting to send an error occured", ex);
            }
            totalBytesSend += count;
        }

//#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER

//        private async void WriteModern(int count)
//        {
//            try
//            {
//                //somehow faster than while loop...
//            Top:
//                totalBytesSend += count;
//                await sessionStream.WriteAsync(new ReadOnlyMemory<byte>(sendBuffer, 0, count)).ConfigureAwait(false);
               
//                if (IsSessionClosing())
//                {
//                    ReleaseSendResourcesIdempotent();
//                    return;
//                }
//                if (messageQueue.TryFlushQueue(ref sendBuffer, 0, out int amountWritten))
//                {
//                    count = amountWritten;
//                    goto Top;

//                }

//                // here there was nothing to flush
//                bool flush = false;

//                enqueueLock.Take();
//                // ask again safely
//                if (messageQueue.IsEmpty())
//                {
//                    messageQueue.Flush();

//                    SendSemaphore.Release();
//                    enqueueLock.Release();
//                    if (IsSessionClosing())
//                        ReleaseSendResourcesIdempotent();
//                    return;
//                }
//                else
//                {
//                    flush = true;

//                }
//                enqueueLock.Release();

//                // something got into queue just before i exit, we need to flush it
//                if (flush)
//                {
//                    if (messageQueue.TryFlushQueue(ref sendBuffer, 0, out int amountWritten_))
//                    {
//                        count = amountWritten_;
//                        goto Top;
//                    }
//                }
//            }
//            catch (Exception e)
//            {
//                HandleError("Error on sent callback ssl", e);
//            }
//        }
//#endif

        private void SentInternal(IAsyncResult ar)
        {
            if (ar.CompletedSynchronously)
            {
                ThreadPool.UnsafeQueueUserWorkItem((s) => Sent(ar), null);
            }
            else
            {
                Sent(ar);
            }
        }

        private void Sent(IAsyncResult ar)
        {
            try
            {
                if (IsSessionClosing())
                {
                    ReleaseSendResourcesIdempotent();
                    return;
                }
                try
                {
                    sessionStream.EndWrite(ar);
                }
                catch (Exception e)
                {
                    HandleError("While attempting to end async send operation on ssl socket, an error occured", e);
                    ReleaseSendResourcesIdempotent();
                    return;
                }

                if (messageQueue.TryFlushQueue(ref sendBuffer, 0, out int amountWritten))
                {
                    WriteOnSessionStream(amountWritten);
                    return;
                }

                // here there was nothing to flush
                bool flush = false;

                enqueueLock.Take();
                // ask again safely
                if (messageQueue.IsEmpty())
                {
                    messageQueue.Flush();

                    SendSemaphore.Release();
                    enqueueLock.Release();
                    if (IsSessionClosing())
                        ReleaseSendResourcesIdempotent();
                    return;
                }
                else
                {
                    flush = true;

                }
                enqueueLock.Release();

                // something got into queue just before i exit, we need to flush it
                if (flush)
                {
                    if (messageQueue.TryFlushQueue(ref sendBuffer, 0, out int amountWritten_))
                    {
                        WriteOnSessionStream(amountWritten_);
                    }
                }
            }
            catch(Exception e)
            {
                HandleError("Error on sent callback ssl", e);
            }

        }

        protected virtual void Receive()
        {
//#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
//            ReceiveNew();
//            return;
//#endif
            if (IsSessionClosing())
            {
                ReleaseReceiveResourcesIdempotent();
                return;
            }
            try
            {
                sessionStream.BeginRead(receiveBuffer, 0, receiveBuffer.Length, Received, null);
            }
            catch (Exception ex)
            {
                HandleError("White receiving from SSL socket an error occurred", ex);
                ReleaseReceiveResourcesIdempotent();
            }
        }
//#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER

//        private async void ReceiveNew()
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
//                    var amountRead = await sessionStream.ReadAsync(receiveMemory).ConfigureAwait(false);
//                    if (amountRead > 0)
//                    {
//                        HandleReceived(receiveBuffer, 0, amountRead);
//                    }
//                    else
//                    {
//                        EndSession();
//                        ReleaseReceiveResourcesIdempotent();
//                    }
//                    totalBytesReceived += amountRead;
//                }
//            }
//            catch (Exception ex)
//            {
//                HandleError("White receiving from SSL socket an error occurred", ex);
//                ReleaseReceiveResourcesIdempotent();
//            }
//        }
//#endif
        protected virtual void Received(IAsyncResult ar)
        {
            if (IsSessionClosing())
            {
                ReleaseReceiveResourcesIdempotent();
                return;
            }

            int amountRead = 0;
            try
            {
                amountRead = sessionStream.EndRead(ar);

            }
            catch (Exception e)
            {
                HandleError("While receiving from SSL socket an exception occurred ", e);
                ReleaseReceiveResourcesIdempotent();
                return;
            }

            if (amountRead > 0)
            {
                HandleReceived(receiveBuffer, 0, amountRead);
            }
            else
            {
                EndSession();
                ReleaseReceiveResourcesIdempotent();
            }
            totalBytesReceived += amountRead;

            // Stack overflow prevention.
            if (ar.CompletedSynchronously)
            {
                ThreadPool.UnsafeQueueUserWorkItem((e) => Receive(), null);
                return;
            }
            Receive();
        }

        protected virtual void HandleReceived(byte[] buffer, int offset, int count)
        {
            totalMessageReceived++;
            OnBytesRecieved?.Invoke(sessionId, buffer, offset, count);

        }

        #region Closure & Disposal
        protected virtual void HandleError(string context, Exception e)
        {
            if (IsSessionClosing())
                return;
            MiniLogger.Log(MiniLogger.LogLevel.Error, "Context : " + context + " Message : " + e.Message);
            EndSession();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool IsSessionClosing()
        {
            return Interlocked.CompareExchange(ref SessionClosing, 1, 1) == 1;
        }

        // This method is Idempotent
        public void EndSession()
        {
            if (Interlocked.CompareExchange(ref SessionClosing, 1, 0) == 0)
            {
                try
                {
                    sessionStream.Close();
                }
                catch { }
                try
                {
                    OnSessionClosed?.Invoke(sessionId);
                }
                catch { }
                OnSessionClosed = null;
                Dispose();
            }

        }

        int sendResReleased = 0;
       protected void ReleaseSendResourcesIdempotent()
        {
            if (Interlocked.CompareExchange(ref sendResReleased, 1, 0) == 0)
            {
                ReleaseSendResources();
            }
        }

        protected virtual void ReleaseSendResources()
        {
            try
            {
                if (UseQueue)
                    BufferPool.ReturnBuffer(sendBuffer);

                Interlocked.Exchange(ref messageQueue, null)?.Dispose();
            }
            catch (Exception e)
            {
                MiniLogger.Log(MiniLogger.LogLevel.Error,
                "Error eccured while releasing ssl session send resources:" + e.Message);
            }
            finally { enqueueLock.Release(); }
           
        }

        int receiveResReleased = 0;

        private void ReleaseReceiveResourcesIdempotent()
        {
            if (Interlocked.CompareExchange(ref receiveResReleased, 1, 0) == 0)
            {
                ReleaseReceiveResources();
            }

        }

        protected virtual void ReleaseReceiveResources()
        {
            BufferPool.ReturnBuffer(receiveBuffer);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref disposedValue,1)== 0)
            {
                try
                {
                    sessionStream.Close();
                    sessionStream.Dispose();
                }
                catch { }

                OnBytesRecieved = null;

                if (!SendSemaphore.IsTaken())
                    ReleaseSendResourcesIdempotent();

                enqueueLock.Release();
                SendSemaphore.Release();
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
        }

        public SessionStatistics GetSessionStatistics()
        {
            var deltaReceived = totalBytesReceived - totalBytesReceivedPrev;
            var deltaSent = totalBytesSend - totalBytesSendPrev;
            totalBytesSendPrev = totalBytesSend;
            totalBytesReceivedPrev = totalBytesReceived;

            long deltaMSgReceived = totalMessageReceived - totalMsgReceivedPrev;
            long deltaMsgSent = messageQueue.TotalMessageDispatched - totalMessageSentPrev;

            totalMsgReceivedPrev = totalMessageReceived;
            totalMessageSentPrev = messageQueue.TotalMessageDispatched;

            return new SessionStatistics(messageQueue.CurrentIndexedMemory,
                (float)(messageQueue.CurrentIndexedMemory / MaxIndexedMemory),
                totalBytesReceived,
                totalBytesSend,
                deltaSent,
                deltaReceived,
                messageQueue.TotalMessageDispatched,
                totalMessageReceived,
                deltaMsgSent,
                deltaMSgReceived);
        }
        #endregion
    }
}
