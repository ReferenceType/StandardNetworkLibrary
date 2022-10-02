using NetworkLibrary.Components;
using NetworkLibrary.TCP.Base;
using NetworkLibrary.Utils;
using System;
using System.Collections.Generic;
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
        protected SemaphoreSlim SendSemaphore = new SemaphoreSlim(1, 1);
        protected SslStream sessionStream;
        protected byte[] receiveBuffer;
        protected byte[] sendBuffer;
        protected Guid sessionId;


        private bool disposedValue;
        private readonly object sendLock = new object();
        private int sendinProgress;
        private BufferProvider bufferProvider;
        private int SessionClosing=0;

        public SslSession(Guid sessionId, SslStream sessionStream, BufferProvider bufferProvider)
        {
            this.sessionId = sessionId;
            this.sessionStream = sessionStream;
            this.bufferProvider = bufferProvider;
        }
        public void StartSession()
        {
            ConfigureBuffers();
            messageQueue = CreateMessageQueue();
            Receive();
        }

        private void ConfigureBuffers()
        {
            receiveBuffer = bufferProvider.GetReceiveBuffer();
            sendBuffer = bufferProvider.GetSendBuffer();
        }

        protected virtual IMessageProcessQueue CreateMessageQueue()
        {
            return new MessageQueue<PlainMessageWriter>(MaxIndexedMemory, new PlainMessageWriter());
        }

        public void SendAsync(byte[] buffer)
        {
            if (IsSessionClosing())
                return;

            lock (sendLock)
            {
                // I dont want to interlock compare exchange here due to expense. it isnt critical because we have the exit lock
                if (Interlocked.CompareExchange(ref sendinProgress,1,1)==1)
                {
                    if (messageQueue.TryEnqueueMessage(buffer))
                        return;
                    // At this point queue is saturated, shall we drop?
                    else if (DropOnCongestion)
                        return;
                    // Otherwise we will wait semaphore
                }
            }
            try
            {
                SendSemaphore.Wait();
            }
            catch { return; }
            Interlocked.Exchange(ref sendinProgress, 1);

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
        }

        private void Sent_(IAsyncResult ar)
        {
            if (ar.CompletedSynchronously)
            {
                ThreadPool.UnsafeQueueUserWorkItem((s) => Sent(ar),null);
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
            catch(Exception e) 
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

            lock (sendLock)
            {
                // ask again safely
                if (messageQueue.IsEmpty())
                {
                    Interlocked.Exchange(ref sendinProgress, 0);
                    //try
                    //{
                        SendSemaphore.Release();
                    //}
                    //catch { }

                    return;
                }
                else
                {
                    flush = true;
                }

            }

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

            int amountRead=0;
            try
            {
                amountRead = sessionStream.EndRead(ar);

            }
            catch(Exception e)
            {
                HandleError("While receiving from SSL socket an exception occured ",e);
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

            // Stack overflow prevention.
            if (ar.CompletedSynchronously)
            {
                ThreadPool.UnsafeQueueUserWorkItem((e) => Receive(),null);
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
#if DEBUG
            throw e;
#endif
            MiniLogger.Log(MiniLogger.LogLevel.Error,"Context : "+context+" Message : "+ e.Message) ;
            EndSession();
        }

        protected bool IsSessionClosing()
        {
            return Interlocked.CompareExchange(ref SessionClosing, 1, 1) == 1;
        }

        // This method is Idempotent
        public void EndSession()
        {
            if(Interlocked.CompareExchange(ref SessionClosing, 1, 0) == 0)
            {
                sessionStream.Close();
                sessionStream.Dispose();
                SendSemaphore.Dispose();
                OnSessionClosed?.Invoke(sessionId);
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
                bufferProvider.ReturnReceiveBuffer(ref receiveBuffer);
                bufferProvider.ReturnSendBuffer(ref sendBuffer);
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
