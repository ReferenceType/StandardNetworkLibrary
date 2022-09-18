using NetworkLibrary.Components.MessageQueue;
using NetworkLibrary.TCP.Base.Interface;
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

        public int SendBufferSize = 128000;
        public int ReceiveBufferSize = 128000;
        public int MaxIndexedMemory = 128000000;

        public event Action<Guid, byte[], int, int> OnBytesRecieved;
        public event Action<Guid> OnSessionClosed;

        protected IMessageQueue messageQueue;
        protected SemaphoreSlim SendSemaphore = new SemaphoreSlim(1, 1);
        protected SslStream sessionStream;
        protected byte[] receiveBuffer;
        protected byte[] sendBuffer;
        protected Guid sessionId;


        private bool disposedValue;
        private readonly object sendLock = new object();
        private int sendinProgress;

        
        public SslSession(Guid sessionId, SslStream sessionStream)
        {
            this.sessionId = sessionId;
            this.sessionStream = sessionStream;

        }

        public void SendAsync(byte[] buffer)
        {
            lock (sendLock)
            {
                // I dont want to interlock compare exchange here due to expense. it isnt critical because we have the exit lock
                if (SendSemaphore.CurrentCount < 1)
                {
                    if (messageQueue.TryEnqueueMessage(buffer))
                        return;
                    else if (DropOnCongestion)
                        return;
                    // Otherwise we will wait semaphore
                    
                }
            }
            SendSemaphore.Wait();
            Interlocked.Exchange(ref sendinProgress, 1);

            messageQueue.TryEnqueueMessage(buffer);
            messageQueue.TryFlushQueue(ref sendBuffer, 0, out int amountWritten);

            // welcome to wonderful world of portability.. This is necessary for .Net6
            // in .Net framework this is executed mostly async, however on others, its mostly sync and it prevents buffering.
            ThreadPool.UnsafeQueueUserWorkItem((s)=> sessionStream.BeginWrite(sendBuffer, 0, amountWritten, Sent, null),null);
            //sessionStream.BeginWrite(sendBuffer, 0, amountWritten, Sent, null);

        }

        private void Sent(IAsyncResult ar)
        {
            sessionStream.Flush();
            sessionStream.EndWrite(ar);

            if (messageQueue.TryFlushQueue(ref sendBuffer, 0, out int amountWritten))
            {
                if (ar.CompletedSynchronously)
                {
                    ThreadPool.UnsafeQueueUserWorkItem((e) => sessionStream.BeginWrite(sendBuffer, 0, amountWritten, Sent, null), null);
                    return;
                }
                sessionStream.BeginWrite(sendBuffer, 0, amountWritten, Sent, null);
                return;
            }

            // here there was nothıng to flush
            bool flush = false;

            lock (sendLock)
            {
                // ask again safely
                if (messageQueue.IsEmpty())
                {
                    Interlocked.Exchange(ref sendinProgress, 0);
                    SendSemaphore.Release();
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
                    if (ar.CompletedSynchronously)
                    {
                        ThreadPool.UnsafeQueueUserWorkItem((e) => sessionStream.BeginWrite(sendBuffer, 0, amountWritten_, Sent, null),null);
                        return;
                    }
                    sessionStream.BeginWrite(sendBuffer, 0, amountWritten_, Sent, null);

                }
            }
        }

        protected virtual void Receive()
        {
            sessionStream.BeginRead(receiveBuffer, 0, receiveBuffer.Length, Received, null);
        }

        protected virtual void Received(IAsyncResult ar)
        {
            int amountRead = sessionStream.EndRead(ar);
            if (amountRead > 0)
            {
                HandleReceived(receiveBuffer, 0, amountRead);
            }
            if (ar.CompletedSynchronously)
            {
                ThreadPool.UnsafeQueueUserWorkItem((e) => Receive(),null);
                return;
            }
            Receive();
        }

        protected virtual void HandleReceived(byte[] buffer, int offset, int count)
        {
            OnBytesRecieved?.Invoke(sessionId, receiveBuffer, offset, count);

        }

        public void StartSession()
        {
            ApplyConfiguration();
            CreateMessageQueue();
            Receive();
        }

        private void ApplyConfiguration()
        {
            receiveBuffer = new byte[SendBufferSize];
            sendBuffer = new byte[ReceiveBufferSize];
        }

        protected virtual void CreateMessageQueue()
        {
            messageQueue = new MessageQueue(MaxIndexedMemory);
        }

        public void EndSession()
        {
            sessionStream.Close();
            OnSessionClosed?.Invoke(sessionId);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    sessionStream.Dispose();
                }
                sendBuffer = null;
                receiveBuffer = null;
                disposedValue = true;
            }
        }

       

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

    }
}
