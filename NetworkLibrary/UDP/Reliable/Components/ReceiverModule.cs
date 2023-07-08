using NetworkLibrary.Components;
using NetworkLibrary.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkLibrary.UDP.Reliable.Components
{
    public class ReceiverModule
    {
        public Action<byte[], int, int> SendFeedback;
        public Action<byte[], int, int> OnMessageReceived;
        internal ConcurrentDictionary<long, Segment> arrived = new ConcurrentDictionary<long, Segment>();

        public bool EnableNacks = true;
        public int NackTriggerThreshold = 1;
        public bool EnablePeriodicSync = true;
        public int syncPeriod = 200;
        public int ReserveforPrefix = 0;

        private PooledMemoryStream pendingMessage = new PooledMemoryStream();
        private long currentSequence = 1;
        private int pendingMessageLenght = 0;
        private int shutdownSignalled = 0;

        private int startSignalled = 0;
        private int executionActive = 0;
        private readonly Spinlock signalLocker = new Spinlock();
        private readonly object nackLock = new object();

        private bool signalled;
        private bool sendSync;
        private int totalReceived = 0;

        private ConcurrentQueue<long> acks = new ConcurrentQueue<long>();
        private System.Threading.Timer timer;
        private System.Threading.Timer syn;
        private WaitCallback consumerLoopCallback;
        public ReceiverModule()
        {
            consumerLoopCallback = ConsumerLoop;
            ConsumerLoop();
            //timer = new System.Threading.Timer(SendAcks, null, 100, 100);
            syn = new System.Threading.Timer(SendSync, null, 500, syncPeriod);
        }

        #region Byte Handling

        public void HandleBytes(byte[] bytes, int offset, int count)
        {
            Interlocked.Add(ref totalReceived, count);
            if (bytes[offset] == SenderModule.Probe1 || bytes[offset] == SenderModule.Probe2)
            {
                HandleProbe(bytes, offset, count);
                return;
            }
            else if (bytes[offset] != SenderModule.Head && bytes[offset] != SenderModule.Chunk)
            {
                MiniLogger.Log(MiniLogger.LogLevel.Error, "Couldnt not handle the bytes on receiver module");
                return;
            }

            int tempOffset = offset;
            tempOffset++;
            var seqNo = PrimitiveEncoder.ReadInt64(bytes, ref tempOffset);
            //Console.WriteLine("Ack Response"+seqNo);
            SendAck(seqNo);

            if (Interlocked.Read(ref currentSequence) > seqNo)
                return;

            var buffer = BufferPool.RentBuffer(count + 256);
            Buffer.BlockCopy(bytes, offset, buffer, 0, count);

            arrived.TryAdd(seqNo, new Segment(buffer, 0, count));
            ConsumerLoop();
        }

        private void ConsumerLoop(object state = null)
        {
            signalLocker.Take();
            {
                Interlocked.Exchange(ref startSignalled, 1);
            }
            signalLocker.Release();
            if (Interlocked.CompareExchange(ref executionActive, 1, 0) == 0)
            {
                Interlocked.Exchange(ref startSignalled, 0);
                try
                {
                Top:
                    // execution block
                    if (shutdownSignalled == 1)
                        return;

                    while (arrived.TryRemove(currentSequence, out var segment))
                    {
                        Interlocked.Increment(ref currentSequence);

                        HandleSequence(segment);
                    }

                    if (!arrived.IsEmpty)
                    {
                        var keys = arrived.Keys;
                        foreach (var item in keys)
                        {
                            if (item < currentSequence)
                            {
                                if (arrived.TryRemove(item, out var segment))
                                {
                                    //  BufferPool.ReturnBuffer(segment.Array);
                                    //  Console.WriteLine("Removed");
                                }

                            }

                        }
                        if (EnableNacks && keys.Count > NackTriggerThreshold)
                        {
                            long next = keys.Min();
                            if (next > currentSequence + 1)
                                SendNack(currentSequence, next);
                        }
                    }

                    // safe exit
                    bool reRun = false;
                    // lock (signalLocker)
                    signalLocker.Take();
                    {
                        if (Interlocked.CompareExchange(ref startSignalled, 0, 1) == 1)
                        {
                            reRun = true;
                        }
                        else
                            Interlocked.Exchange(ref executionActive, 0);

                    }
                    signalLocker.Release();
                    if (reRun)
                    {
                        reRun = false;
                        goto Top;
                        //Interlocked.Exchange(ref executionActive, 0);
                        //ThreadPool.UnsafeQueueUserWorkItem(consumerLoopCallback, null);
                    }

                }
                catch (Exception ex)
                {
                    MiniLogger.Log(MiniLogger.LogLevel.Error,
                        $"Critical errror occured on reliable udp receiver module: {ex.Message} Trace:{ex.StackTrace}");
                }
            }
        }

        #endregion

        #region Message Extraction
        // From this point everything is syncronus and in order.
        private void HandleSequence(Segment segment)
        {
            // remove opcode from this point
            var bytes = segment.Array;
            var offset = segment.Offset;
            var count = segment.Count - 1;

            switch (bytes[offset++])
            {
                case SenderModule.Head:
                    HandleHead(bytes, offset, count);
                    break;
                case SenderModule.Chunk:
                    HandleChunk(bytes, offset, count);
                    break;
            }
            BufferPool.ReturnBuffer(bytes);

        }

        private void HandleChunk(byte[] bytes, int offset, int count)
        {
            // Offset the info frame
            int originalOffset = offset;
            PrimitiveEncoder.ReadInt64(bytes, ref offset);//sqn, need the actual offset.
            count -= offset - originalOffset;

            pendingMessage.Write(bytes, offset, count);
            //BufferPool.ReturnBuffer(bytes);

            if (pendingMessageLenght == pendingMessage.Position32)
            {
                OnMessageReceived?.Invoke(pendingMessage.GetBuffer(), 0, pendingMessage.Position32);
                sendSync = false;
                pendingMessage.Clear();
            }
        }

        private void HandleHead(byte[] bytes, int offset, int count)
        {
            int originalOffset = offset;
            var seqNo = PrimitiveEncoder.ReadInt64(bytes, ref offset);
            int msgLen = PrimitiveEncoder.ReadInt32(bytes, ref offset);
            count -= offset - originalOffset;
            if (msgLen <= count)
            {
                OnMessageReceived?.Invoke(bytes, offset, count);
                //BufferPool.ReturnBuffer(bytes);
            }
            else
            {
                sendSync = true;

                pendingMessageLenght = msgLen;
                pendingMessage.Write(bytes, offset, count);
                // BufferPool.ReturnBuffer(bytes);
            }

        }
        #endregion

        #region Feedbacks

        Stopwatch sw = Stopwatch.StartNew();
        private void SendAck(long seqNo)
        {
            // test stuff
            //bool state = false;///sw.ElapsedMilliseconds > 20000;
            //if (state)
            //{
            //    acks.Enqueue(seqNo);
            //    signalled = true;
            //    if (sw.ElapsedMilliseconds > 40000)
            //        sw.Restart();
            //    return;
            //}

            var stream = SharerdMemoryStreamPool.RentStreamStatic();
            stream.Advance(ReserveforPrefix);
            stream.WriteByte(SenderModule.Ack);
            PrimitiveEncoder.WriteInt64(stream, seqNo);
            SendFeedback?.Invoke(stream.GetBuffer(), 0, stream.Position32);
            SharerdMemoryStreamPool.ReturnStreamStatic(stream);
        }
        Random r = new Random();
        private void SendNack(long fromSeq, long toSeq)
        {
            if (toSeq <= fromSeq)
                return;

            var stream = SharerdMemoryStreamPool.RentStreamStatic();
            stream.Advance(ReserveforPrefix);
            stream.WriteByte(SenderModule.Nack);

            PrimitiveEncoder.WriteInt64(stream, fromSeq);
            PrimitiveEncoder.WriteInt64(stream, toSeq);

            SendFeedback?.Invoke(stream.GetBuffer(), 0, stream.Position32);
            SharerdMemoryStreamPool.ReturnStreamStatic(stream);
        }

        private void SendSync(object state)
        {
            var sends = Interlocked.Exchange(ref totalReceived, 0);
            var receiveRate = sends * (1000 / syncPeriod);

            if (!EnablePeriodicSync) return;
            if (!sendSync) return;

            var stream = SharerdMemoryStreamPool.RentStreamStatic();
            stream.Advance(ReserveforPrefix);
            stream.WriteByte(SenderModule.SynAck);
            var currSeq = Interlocked.Read(ref currentSequence);

            PrimitiveEncoder.WriteInt64(stream, currSeq);
            PrimitiveEncoder.WriteInt64(stream, receiveRate);

            SendFeedback?.Invoke(stream.GetBuffer(), 0, stream.Position32);
            SharerdMemoryStreamPool.ReturnStreamStatic(stream);
        }
        #endregion

        #region Experimental
        double ts = 0;
        private void HandleProbe(byte[] bytes, int offset, int count)
        {
            if (bytes[offset++] == SenderModule.Probe1)
            {
                Interlocked.Exchange(ref ts, sw.Elapsed.TotalMilliseconds * 1000000);
            }
            else
            {
                var curr = sw.Elapsed.TotalMilliseconds * 1000000;
                var dt = curr - Interlocked.CompareExchange(ref ts, 0, 0);

                var stream = SharerdMemoryStreamPool.RentStreamStatic();
                stream.Advance(ReserveforPrefix);

                stream.WriteByte(SenderModule.ProbeResult);
                PrimitiveEncoder.WriteDouble(stream, dt);

                SendFeedback?.Invoke(stream.GetBuffer(), 0, stream.Position32);
                SharerdMemoryStreamPool.ReturnStreamStatic(stream);
            }
        }
        private void SendAcks(object state)
        {
            if (!signalled) return;
            signalled = false;
            //Console.WriteLine("Acklist");
            try
            {
                var stream = SharerdMemoryStreamPool.RentStreamStatic();
                stream.Advance(ReserveforPrefix);
                stream.WriteByte(SenderModule.AckList);
                PrimitiveEncoder.WriteInt64(stream, currentSequence);
                HashSet<long> set = new HashSet<long>();
                while (acks.TryDequeue(out long seqNo))
                {
                    set.Add(seqNo);
                }

                PrimitiveEncoder.WriteInt32(stream, set.Count);
                foreach (var sq in set)
                {
                    PrimitiveEncoder.WriteInt64(stream, sq);
                }
                SendFeedback?.Invoke(stream.GetBuffer(), 0, stream.Position32);
                SharerdMemoryStreamPool.ReturnStreamStatic(stream);
            }
            catch { }

        }
        #endregion

        public void ShutDown()
        {
            Interlocked.Exchange(ref shutdownSignalled, 1);
        }

    }
}
