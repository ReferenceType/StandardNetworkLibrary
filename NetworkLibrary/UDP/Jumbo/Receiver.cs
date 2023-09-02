using NetworkLibrary.Components;
using NetworkLibrary.Utils;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace NetworkLibrary.UDP.Jumbo
{
    class PendingMessage
    {
        public byte[] msgBuffer;
        public int MessageNo;
        int lifetime;
        int[] written;
        int remaining;
        public int totalBytes;
        int completion;
        int totalSequences;
        public Action<PendingMessage> Completed;
        public Action<PendingMessage> TimedOut;
        public PendingMessage( int lifetime, byte totalSequences, int messageNo)
        {
            MessageNo = messageNo;
            this.totalSequences = totalSequences;
            this.lifetime = lifetime;
            msgBuffer = BufferPool.RentBuffer(totalSequences * 64000);
            written = new int[totalSequences];
            remaining = totalSequences;
        }

        // this appends are concurrent
        public void Append(byte seqNo, byte[] buffer, int offset, int count)
        {
            if(Interlocked.CompareExchange(ref completion, 0, 0) == 1) { return; }

            if (Interlocked.CompareExchange(ref written[seqNo - 1], 1, 0) == 0)
            {
                int offs = (seqNo - 1) * 64000;
                ByteCopy.BlockCopy(buffer, offset, msgBuffer, offs,count);

                if (seqNo == totalSequences)
                {
                    int totalbytes = ((totalSequences - 1) * 64000) + count;
                    Interlocked.Exchange(ref totalBytes, totalbytes);
                }
                lifetime += 1000;

                if (Interlocked.Decrement(ref remaining) == 0)
                {
                    // complete, this check is for timeout collission
                    if (Interlocked.CompareExchange(ref completion, 1, 0) == 0)
                    {
                        Completed?.Invoke(this);
                        TimedOut = null;
                        Completed = null;
                    }
                }
            }

        }

        public void Tick(int dt)
        {
            lifetime -= dt;
            if (lifetime - dt < 0)
            {
                OnTimeout();
            }
        }
        protected void OnTimeout()
        {
            if (Interlocked.CompareExchange(ref completion, 1, 0) == 0)
            {
                TimedOut?.Invoke(this);
                TimedOut = null;
                Completed = null;
            }
        }

    }

    internal class Receiver
    {
        public Action<byte[], int, int> OnMessageExtracted;
        private ConcurrentDictionary<int, PendingMessage> pendingMessages = new ConcurrentDictionary<int, PendingMessage>();
        private System.Threading.Timer timer;
        SpinLock sp = new SpinLock();

        private void OnTick(object state)
        {

            foreach (var message in pendingMessages)
            {
                try
                {
                    message.Value.Tick(1000);
                }
                catch{ }
            }
        }

        public Receiver()
        {
            timer = new System.Threading.Timer(OnTick, null, 1000, 1000);
        }

        private object locker = new object();
        public void ProccesReceivedDatagram(byte[] buffer, int offset, int count)
        {
            PendingMessage pend = null;

            int originalOffset = offset;
            int msgNo = PrimitiveEncoder.ReadInt32(buffer, ref offset);
            byte totNumSeq = buffer[offset++];
            byte currenSeq = buffer[offset++];
            count = count - (offset - originalOffset);

           
            bool taken = false;
            Monitor.Enter(locker, ref taken);
            { 
                if (!pendingMessages.TryGetValue(msgNo, out pend))
                {
                    pend = new PendingMessage( 5000, totNumSeq, msgNo);
                    pendingMessages.TryAdd(msgNo, pend);
                    pend.TimedOut = MessagetimedOut;
                    pend.Completed = MessageComplete;
                }
            }
            if (taken)
                Monitor.Exit(locker);
           
            pend.Append(currenSeq, buffer, offset, count);

        }

        internal void MessageComplete(PendingMessage message)
        {
            // lock (locker)
            pendingMessages.TryRemove(message.MessageNo, out var pend);
            OnMessageExtracted?.Invoke(message.msgBuffer, 0, message.totalBytes);
            BufferPool.ReturnBuffer(message.msgBuffer);

        }

        internal void MessagetimedOut(PendingMessage message)
        {
           lock (locker)
                pendingMessages.TryRemove(message.MessageNo, out _);

            BufferPool.ReturnBuffer(message.msgBuffer);
        }

        internal void Release()
        {
            timer.Dispose();
            OnMessageExtracted = null;
            pendingMessages.Clear();
        }
    }
}
