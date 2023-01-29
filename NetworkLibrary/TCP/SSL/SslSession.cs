﻿using NetworkLibrary.Components;
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
    public class SslSession : IAsyncSession
    {
        public bool DropOnCongestion { get; internal set; }
        public int MaxIndexedMemory = 128000000;

        public event Action<Guid, byte[], int, int> OnBytesRecieved;
        public event Action<Guid> OnSessionClosed;

        protected IMessageQueue messageQueue;

        protected Spinlock SendSemaphore = new Spinlock();
        protected Spinlock enqueueLock = new Spinlock();
        protected SslStream sessionStream;
        protected byte[] receiveBuffer;
        protected byte[] sendBuffer;
        protected Guid sessionId;

        public int SendBufferSize = 128000;
        public int ReceiveBufferSize = 128000;

        protected IPEndPoint RemoteEP;
        public IPEndPoint RemoteEndpoint { get => RemoteEP;  set => RemoteEP = value; }

        private bool disposedValue;
        //private readonly object sendLock = new object();

        private int SessionClosing = 0;

        internal bool UseQueue = true;
        private long totalBytesSend;
        private long totalBytesReceived;
        private long totalMessageReceived=0;

        private long totalBytesSendPrev = 0;
        private long totalBytesReceivedPrev = 0;
        private long totalMsgReceivedPrev;
        private long totalMessageSentPrev;

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

        protected virtual void ConfigureBuffers()
        {
            receiveBuffer = BufferPool.RentBuffer(ReceiveBufferSize);
            if(UseQueue) sendBuffer = BufferPool.RentBuffer(SendBufferSize);

        }

        protected virtual IMessageQueue CreateMessageQueue()
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
            try
            {
                SendAsync_(buffer, offset, count);
            }
            catch { if (!IsSessionClosing()) throw; }
        }
        private void SendAsync_(byte[] buffer, int offset, int count)
        {
            enqueueLock.Take();
            
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
                SendSemaphore.Release();
                return;
            }

            // you have to push it to queue because queue also does the processing.
            messageQueue.TryEnqueueMessage(buffer, offset, count);
            messageQueue.TryFlushQueue(ref sendBuffer, 0, out int amountWritten);
            WriteOnSessionStream(amountWritten);
            enqueueLock.Release();

        }
        public void SendAsync(byte[] buffer)
        {
            if (IsSessionClosing())
                return;
            try
            {
                SendAsync_(buffer);
            }
            catch { if (!IsSessionClosing()) throw; }
        }
        private void SendAsync_(byte[] buffer)
        {
            enqueueLock.Take();

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
                SendSemaphore.Release();
                return;
            }

            // you have to push it to queue because queue also does the processing.
            messageQueue.TryEnqueueMessage(buffer);
            messageQueue.TryFlushQueue(ref sendBuffer, 0, out int amountWritten);
            WriteOnSessionStream(amountWritten);
            enqueueLock.Release();

        }

        protected void WriteOnSessionStream(int count)
        {
            try
            {
                sessionStream.BeginWrite(sendBuffer, 0, count, Sent_, null);
            }
            catch (Exception ex)
            {
                HandleError("While attempting to send an error occured", ex);
            }
             totalBytesSend+=count;
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
            {
                return;
            }
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
            {
                ReleaseReceiveResources();
                return;
            }
            try
            {
                sessionStream.BeginRead(receiveBuffer, 0, receiveBuffer.Length, Received, null);

            }
            catch (Exception ex)
            {
                HandleError("White receiving from SSL socket an error occured", ex);
                ReleaseReceiveResources();
            }
        }

        protected virtual void Received(IAsyncResult ar)
        {
            if (IsSessionClosing())
            {
                ReleaseReceiveResources();
                return;
            }

            int amountRead = 0;
            try
            {
                amountRead = sessionStream.EndRead(ar);

            }
            catch (Exception e)
            {
                HandleError("While receiving from SSL socket an exception occured ", e);
                ReleaseReceiveResources();
                return;
            }

            if (amountRead > 0)
            {
                HandleReceived(receiveBuffer, 0, amountRead);
            }
            else
            {
                EndSession();
                ReleaseReceiveResources();
            }
            totalBytesReceived+= amountRead;

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

        #region Closure
        protected virtual void HandleError(string context, Exception e)
        {
            MiniLogger.Log(MiniLogger.LogLevel.Error, "Context : " + context + " Message : " + e.Message);
            EndSession();
        }

        protected bool IsSessionClosing()
        {
            return Interlocked.CompareExchange(ref SessionClosing, 1, 1) == 1;
           // return Volatile.Read(ref SessionClosing) == 1;
        }

        // This method is Idempotent
        public void EndSession()
        {
            if (Interlocked.CompareExchange(ref SessionClosing, 1, 0) == 0)
            {
                sessionStream.Close();
                OnSessionClosed?.Invoke(sessionId);
                Dispose();
            }

        }

        int sendResReleased = 0;
        private void ReleaseSendResources()
        {
            if (Interlocked.CompareExchange(ref sendResReleased, 1, 0) == 0)
            {
                if (UseQueue) BufferPool.ReturnBuffer(sendBuffer);

                messageQueue?.Dispose();
                //messageQueue = null;
                enqueueLock.Release();
            }
        }

        int receiveResReleased = 0;
        private void ReleaseReceiveResources()
        {
            if (Interlocked.CompareExchange(ref receiveResReleased,1,0)==0)
            {
                BufferPool.ReturnBuffer(receiveBuffer);
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

                OnBytesRecieved= null;
                OnSessionClosed = null;
               
                sessionStream = null;
                ReleaseSendResources();
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