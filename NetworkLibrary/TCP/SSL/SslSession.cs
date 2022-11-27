using NetworkLibrary.Components;
using NetworkLibrary.Components.MessageBuffer;
using NetworkLibrary.Components.Statistics;
using NetworkLibrary.TCP.Base;
using NetworkLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Net.Security;
using System.Text;
using System.Threading;

namespace NetworkLibrary.TCP.SSL.Base
{
    internal class SslSession : IAsyncSession
    {
        public bool DropOnCongestion { get; internal set; }
        public int MaxIndexedMemory = 128000000;

        public event Action<Guid, byte[], int, int> OnBytesRecieved;
        public event Action<Guid> OnSessionClosed;

        protected IMessageProcessQueue messageQueue;

        private Spinlock SendSemaphore = new Spinlock();
        private Spinlock enqueueLock = new Spinlock();
        protected SslStream sessionStream;
        protected byte[] receiveBuffer;
        protected byte[] sendBuffer;
        protected byte[] sendBuffer_;
        protected Guid sessionId;

        public int SendBufferSize = 128000;
        public int ReceiveBufferSize = 128000;

        private IPEndPoint RemoteEP;
        public IPEndPoint RemoteEndpoint { get => RemoteEP; internal set => RemoteEP = value; }

        private bool disposedValue;
        //private readonly object sendLock = new object();

        private int SessionClosing = 0;

        internal bool UseQueue = true;
        private long totalBytesSend;
        private long totalBytesReceived;

        public SslSession(Guid sessionId, SslStream sessionStream)
        {
            this.sessionId = sessionId;
            this.sessionStream = sessionStream;
        }
        public void StartSession()
        {
            ConfigureBuffers();
            messageQueue = CreateMessageQueue();
            Receive();
        }

        private void ConfigureBuffers()
        {
            receiveBuffer = BufferPool.RentBuffer(ReceiveBufferSize);
            sendBuffer = BufferPool.RentBuffer(SendBufferSize);
            sendBuffer_ = sendBuffer;
        }

        protected virtual IMessageProcessQueue CreateMessageQueue()
        {
            if (UseQueue)
                return new MessageQueue<UnsafePlainMessageWriter>(MaxIndexedMemory, new UnsafePlainMessageWriter());
            else
                return new MessageBuffer(MaxIndexedMemory, writeLengthPrefix: false);

        }
        public void SendAsync(byte[] buffer, int offset, int count)
        {
            if (IsSessionClosing())
                return;

            enqueueLock.Take();
            if (SendSemaphore.IsTaken())
            {
                if (messageQueue.TryEnqueueMessage(buffer, offset, count))
                {
                    enqueueLock.Release();
                    return;
                }
                // At this point queue is saturated, shall we drop?
                else if (DropOnCongestion)
                {
                    enqueueLock.Release();
                    return;
                }

                // Otherwise we will wait semaphore
            }
            enqueueLock.Release();

            SendSemaphore.Take();
            if (IsSessionClosing())
            {
                SendSemaphore.Release();
                return;
            }

            // you have to push it to queue because queue also does the processing.
            messageQueue.TryEnqueueMessage(buffer, offset, count);
            messageQueue.TryFlushQueue(ref sendBuffer, 0, out int amountWritten);
            WriteOnSessionStream(amountWritten);
        }
        public void SendAsync(byte[] buffer)
        {
            if (IsSessionClosing())
                return;

            // avoiding try finally;
            enqueueLock.Take();
            if (SendSemaphore.IsTaken())
            {
                if (messageQueue.TryEnqueueMessage(buffer))
                {
                    enqueueLock.Release();
                    return;
                }
                // At this point queue is saturated, shall we drop?
                else if (DropOnCongestion)
                {
                    enqueueLock.Release();
                    return;
                }
                // Otherwise we will wait semaphore
            }
            enqueueLock.Release();

            SendSemaphore.Take();
            if (IsSessionClosing())
            {
                SendSemaphore.Release();
                return;
            }

            // you have to push it to queue because queue also does the processing.
            messageQueue.TryEnqueueMessage(buffer);
            messageQueue.TryFlushQueue(ref sendBuffer, 0, out int amountWritten);
            WriteOnSessionStream(amountWritten);

        }

        private void WriteOnSessionStream(int count)
        {
            try
            {
                sessionStream.BeginWrite(sendBuffer, 0, count, Sent_, null);
            }
            catch (Exception ex)
            {
                HandleError("While attempting to send an error occured", ex);
            }
            Interlocked.Add(ref totalBytesSend, count);
        }

        private void Sent_(IAsyncResult ar)
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
            if (IsSessionClosing())
                return;

            try
            {
                sessionStream.EndWrite(ar);
            }
            catch (Exception e)
            {
                HandleError("While attempting to end async send operation on ssl socket, an error occured", e);
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

        protected virtual void Receive()
        {
            if (IsSessionClosing())
                return;
            try
            {
                sessionStream.BeginRead(receiveBuffer, 0, receiveBuffer.Length, Received, null);

            }
            catch (Exception ex)
            {
                HandleError("White receiving from SSL socket an error occured", ex);
            }
        }

        protected virtual void Received(IAsyncResult ar)
        {
            if (IsSessionClosing())
                return;

            int amountRead = 0;
            try
            {
                amountRead = sessionStream.EndRead(ar);

            }
            catch (Exception e)
            {
                HandleError("While receiving from SSL socket an exception occured ", e);
                return;
            }

            if (amountRead > 0)
            {
                HandleReceived(receiveBuffer, 0, amountRead);
            }
            else
            {
                EndSession();
            }
            Interlocked.Add(ref totalBytesReceived, amountRead);

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
            OnBytesRecieved?.Invoke(sessionId, buffer, offset, count);

        }

        #region Closure
        protected virtual void HandleError(string context, Exception e)
        {
            MiniLogger.Log(MiniLogger.LogLevel.Error, "Context : " + context + " Message : " + e.Message);
            EndSession();
        }

        protected bool IsSessionClosing()
        {
            return Interlocked.CompareExchange(ref SessionClosing, 1, 1) == 1;
        }

        // This method is Idempotent
        public void EndSession()
        {
            if (Interlocked.CompareExchange(ref SessionClosing, 1, 0) == 0)
            {
                sessionStream.Close();
                sessionStream.Dispose();

                // SendSemaphore.Dispose();
                OnSessionClosed?.Invoke(sessionId);
                messageQueue = null;
                Dispose();
            }

        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    sessionStream.Dispose();
                }
                disposedValue = true;
                BufferPool.ReturnBuffer(receiveBuffer);
                BufferPool.ReturnBuffer(sendBuffer_);
                messageQueue?.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
        }

        public SessionStatistics GetSessionStatistics()
        {
            return new SessionStatistics(messageQueue.CurrentIndexedMemory,
                (float)(messageQueue.CurrentIndexedMemory / MaxIndexedMemory),
                totalBytesSend,
                totalBytesReceived,
                messageQueue.TotalMessageDispatched);
        }
        #endregion
    }
}
