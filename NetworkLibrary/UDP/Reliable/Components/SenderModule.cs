//#define EnableDebugLogs
using NetworkLibrary.Components;
using NetworkLibrary.UDP.Reliable.Config;
using NetworkLibrary.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace NetworkLibrary.UDP.Reliable.Components
{
    #region Helpers
    internal class ScheduledExecution : TimedObject
    {
        private int delay;
        private Action execution;
        private bool cancelled;

        public ScheduledExecution(int delayMs, Action execution)
        {
            delay = delayMs;
            this.execution = execution;
            cancelled = true;
        }
        public ScheduledExecution Set(int delayMs, Action execution)
        {
            delay = delayMs;
            this.execution = execution;
            cancelled = false;
            return this;
        }

        public override void Tick(int dt)
        {
            if (cancelled) return;
            delay -= Timer.TickFrequency;
            if (delay < 0)
            {
                ThreadPool.UnsafeQueueUserWorkItem(Timeout, null);

            }
        }
        private void Timeout(object state)
        {
            cancelled = true;
            base.OnTimeout();
            execution?.Invoke();
            execution = null;

        }

        internal void Cancel()
        {
            if (cancelled) return;
            cancelled = true;
            Timer.Remove(this);
        }
    }

    internal class Package : TimedObject
    {
        public PooledMemoryStream Stream;
        public long SeqNo;
        public long CreationTime;
        public bool complete = false;

        private SenderModule sender;
        private int lifetime = 9999;
        internal int resendMultplier = 1;
        public bool IsReleased => Volatile.Read(ref released) == 1;
        int released = 0;

        public Package()
        {

        }

        public void Set(PooledMemoryStream stream, long seqNo, SenderModule sender)
        {
            Stream = stream;
            SeqNo = seqNo;
            this.sender = sender;
            released = 0;
            complete = false;
            resendMultplier = 1;
            lifetime = 9999;
        }

        protected override void OnTimeout()
        {
            if (complete) return;
            complete = true;

            if (IsReleased)
                return;

            ThreadPool.UnsafeQueueUserWorkItem((s) => sender.PacketTimedOut(this), null);


        }

        internal void SetTimeout(int timeOut)
        {
            complete = false;
            CreationTime = sender.GetTime();
            lifetime = Math.Min(2 * sender.MaxRTO, Math.Max(sender.MinRTO, resendMultplier * timeOut));
        }

        public override void Tick(int dt)
        {
            if (complete) return;
            if (IsReleased) return;

            if (sender.GetTime() - CreationTime >= lifetime)
                OnTimeout();
        }

        public void Release()
        {
            Interlocked.Exchange(ref released, 1);
            complete = true;
        }
    }
    #endregion

    public class SenderModule
    {
        public const byte Head = 1;
        public const byte Chunk = 2;
        public const byte Ack = 3;
        public const byte Nack = 4;
        public const byte AckList = 5;
        public const byte Probe1 = 6;
        public const byte Probe2 = 7;
        public const byte ProbeResult = 8;
        public const byte SynAck = 9;

        public int MaxSegmentSize = 64000;
        public int MaxWindowSize = 100_000_000;
        public int MinWindowSize = 64000;

        public int MaxRTO = 3000;
        public int RTOOffset = 20;
        public int MinRTO = 300;

        public float WindowIncrementMultiplier = 1;
        public float TimeoutWindowTrim = 0.80f;
        public float SoftwindowTrim = 0.95f;
        public float PacingThreshold = 0.5f;
        public int WindowBumpLowerTreshold = 50;
        public bool EnableWindowBump = true;
        public int ReserveForPrefix = 0;

        public Action<byte[], int, int> SendRequested;

        private ConcurrentQueue<Package> SendQueue = new ConcurrentQueue<Package>();
        private ConcurrentQueue<Package> PriorityQueue = new ConcurrentQueue<Package>();
        internal ConcurrentDictionary<long, Package> pendingPackages = new ConcurrentDictionary<long, Package>();
        private Stopwatch stopwatch = Stopwatch.StartNew();

        private int WindowSize = 64000;
        private int PendingBytes = 0;
        private long currentSeqNo = 0;
        private int producerSignalled = 0;
        private int producerExecuting = 0;
        private object reSenderLock = new object();
        private object producerLock = new object();
        private int shutdownSignalled;

        // TCP metrics
        private float RTT = 50;
        internal float RTO = 300;
        private float Mdev = 1;
        int totalDispatchedBytes = 0;
        float sendRate = 0;
        int packetsSent;
        int packageRate;
        int packetsReSent;
        int lossRate;
        Random r;
        ScheduledExecution delayedExecution;
        Action forcedLazyExecution;
        Action lazyExecution;
        //bool debug = false;
        public SenderModule(SenderModuleConfig config = null)
        {
            //if (config == null)
            //{
            //    config = new SenderModuleConfig();
            //}
            //  ApplyConfig(config);
            r = new Random(GetHashCode());
            delayedExecution = new ScheduledExecution(10, StartProducer);
            forcedLazyExecution = () => { Interlocked.Exchange(ref producerExecuting, 0); StartProducer(); };
            lazyExecution = () => { StartProducer(); };
#if EnableDebugLogs

            Task.Run(async () =>
            {
                int cnt = 0;
                while (true)
                {
                    int delay = 1000;//r.Next(900,1000);

                    await Task.Delay(delay);
                    sendRate = Interlocked.Exchange(ref totalDispatchedBytes, 0) * (1000 / delay);
                    packageRate = Interlocked.Exchange(ref packetsSent, 0) * (1000 / delay);
                    lossRate = Interlocked.Exchange(ref packetsReSent, 0) * (1000 / delay);



                    //if (GetTime() - lastProbeTime > 1000)
                    //{
                    //    SendProbes();
                    //}
                    // return;
                    if (SendQueue.Count == 0)
                        continue;
                    Console.WriteLine("--------------------------Rates----------------------------------");
                    Console.WriteLine("PackageSendRate " + packageRate);
                    Console.WriteLine("Loss Rate       " + lossRate);
                    Console.WriteLine("---------------------------  ---------------------------------");
                    Console.WriteLine("pending packages : " + pendingPackages.Count);
                    Console.WriteLine("SQ : " + SendQueue.Count);
                    Console.WriteLine("PQ : " + PriorityQueue.Count);
                    Console.WriteLine("WS : " + WindowSize);
                    Console.WriteLine("Pending Bytes : " + PendingBytes);
                    Console.WriteLine("SendRate : [" + sendRate.ToString("N0") + "");
                    Console.WriteLine("Estimated BW : [" + estimatedBandwidth.ToString("N0") + "");
                    Console.WriteLine("------------------------------------------------------------");

        }

    });

#endif

        }


        private void ApplyConfig(SenderModuleConfig config)
        {
            MaxSegmentSize = config.MaxSegmentSize;
            MaxRTO = config.MaxRTO;
            MinRTO = config.MinRTO;
            MaxWindowSize = config.MaxWindowSize;
            MinWindowSize = config.MinWindowSize;
            WindowIncrementMultiplier = config.WindowIncrementMultiplier;
            EnableWindowBump = config.EnableWindowBump;
            WindowBumpLowerTreshold = config.WindowBumpLowerTreshold;
            WindowIncrementMultiplier = config.WindowIncrementMultiplier;
            TimeoutWindowTrim = config.TimeoutWindowTrim;
            SoftwindowTrim = config.SoftwindowTrim;
            PacingThreshold = config.PacingThreshold;

        }

        #region Sender System

        private void StartProducer()
        {
            lock (producerLock)
            {
                Interlocked.Exchange(ref producerSignalled, 1);
            }
            if (Interlocked.CompareExchange(ref producerExecuting, 1, 0) == 0)
            {
                delayedExecution.Cancel();
                if (shutdownSignalled == 1)
                    return;
                Interlocked.Exchange(ref producerSignalled, 0);

                bool immediateSchedule = false;
                bool delayedSchedule = false;
                bool force = false;
                int delay = 10;

                try
                {
                Top:
                    int Windowsize = Math.Max(WindowSize, MinWindowSize);
                    while (PriorityQueue.TryDequeue(out var segment))
                    {
                        ResendSegment(segment);
                        Windowsize = Math.Max(WindowSize, MinWindowSize);

                        if (PendingBytes > MinWindowSize)
                        {
                            delayedSchedule = true;
                            delay = (int)RTT;
                            force = false;
                            break;
                        }
                    }

                    Windowsize = Math.Max(WindowSize, MinWindowSize);
                    if (PendingBytes > Windowsize)
                    {
                        delayedSchedule = true;
                    }
                    else
                    {
                        while (SendQueue.TryDequeue(out var segment))
                        {
                            SendSegment(segment);


                            Windowsize = Math.Max(WindowSize, MinWindowSize);
                            var pb = PendingBytes;
                            if (pb > PacingThreshold * Windowsize)
                            {
                                delayedSchedule = true;
                               // force = true;
                                break;
                            }

                            if (!PriorityQueue.IsEmpty)
                            {
                                immediateSchedule = true;
                                // goto Top;
                                break;
                            }
                        }

                    }
                    // exit strategy
                    lock (producerLock)
                    {
                        if (delayedSchedule)
                        {


                            if (force)
                            {
                                // Console.WriteLine("Foereced");
                                Timer.Register(delayedExecution.Set(delay, forcedLazyExecution));
                                return;
                            }
                            else
                            {
                                Timer.Register(delayedExecution.Set(delay, lazyExecution));
                                Interlocked.Exchange(ref producerExecuting, 0);
                                return;
                            }

                        }
                        // someone signalled?
                        else if (Interlocked.CompareExchange(ref producerSignalled, 0, 1) == 1)
                        {
                            immediateSchedule = true;
                        }
                        else
                        {
                            Interlocked.Exchange(ref producerExecuting, 0);

                        }


                    }

                    if (immediateSchedule)
                    {
                        immediateSchedule = false;
                        goto Top;
                        //Interlocked.Exchange(ref producerExecuting, 0);
                        //ThreadPool.UnsafeQueueUserWorkItem((x) => StartProducer(), null);
                    }
                }
                catch (Exception ex)
                {
                    MiniLogger.Log(MiniLogger.LogLevel.Error,
                    $"Error occured on rudp sender execution loop: {ex.Message}");
                }

            }
        }

        // called once per segment.
        private void SendSegment(Package segment)
        {
            lock (reSenderLock)
            {
                if (pendingPackages.TryAdd(segment.SeqNo, segment))
                {
                    Interlocked.Add(ref PendingBytes, segment.Stream.Position32);
                    Interlocked.Increment(ref packetsSent);
                    RequestSend(segment, register: true);
                }
            }
        }

        private void ResendSegment(Package package)
        {
            lock (reSenderLock)
            {
                if (pendingPackages.TryGetValue(package.SeqNo, out var segment))
                {
                    segment.resendMultplier++;
                    RequestSend(segment);
                    Interlocked.Increment(ref packetsReSent);
#if EnableDebugLogs
                    Console.WriteLine("RST " + segment.SeqNo);
#endif
                }

            }
        }
        // Only here segment goes to socket;
        private void RequestSend(Package segment, bool register = false)
        {
            if (segment.IsReleased)
            {
                Console.WriteLine($"Released Error !! Seq {segment.SeqNo} Min   {pendingPackages.Keys.Min()} || MAx {pendingPackages.Keys.Max()}");
                return;
            }

            segment.SetTimeout((int)RTO + RTOOffset + 1);


            SendRequested?.Invoke(segment.Stream.GetBuffer(), 0, segment.Stream.Position32);
            Interlocked.Add(ref totalDispatchedBytes, segment.Stream.Position32);
            if (register)
                if (!Timer.Register(segment))
                    Console.WriteLine($"Seq {segment.SeqNo} Min  | {pendingPackages.Keys.Min()} || MAx {pendingPackages.Keys.Max()}");
        }
        #endregion

        #region Input Bytes

        private readonly object sendLock = new object();
        //User Space
        public void ProcessBytesToSend(byte[] buffer, int offset, int count)
        {
            lock (sendLock)
            {
                FrameChunkEnqueue(buffer, offset, count);
            }
            StartProducer();


        }
        public void ProcessBytesToSend(in Segment first, in Segment second)
        {
            lock (sendLock)
            {
                FrameChunkEnqueue(first, second);
            }
            StartProducer();


        }

        /**********************************************
         * [Head][sqeNo][messageLenght][chunk(64000)] +
         * [Chunk][seqNo][chunk(64000)] +
         * [Chunk][seqNo][chunk(64000)] +
         * [Chunk][seqNo][chunk(1805)] 
         ********************************************/
        private void FrameChunkEnqueue(byte[] buffer, int offset, int count)
        {
            var stream = GetStream();
            stream.Advance(ReserveForPrefix);

            stream.WriteByte(Head);
            var sqn = Interlocked.Increment(ref currentSeqNo);
            PrimitiveEncoder.WriteInt64(stream, sqn);
            PrimitiveEncoder.WriteInt32(stream, count);
            if (count < MaxSegmentSize)
            {
                stream.Write(buffer, offset, count);
                count = 0;
            }
            else
            {
                stream.Write(buffer, offset, MaxSegmentSize);
                offset += MaxSegmentSize;
                count -= MaxSegmentSize;

            }
            SendQueue.Enqueue(GetPackage(stream, sqn, this));
            while (count > MaxSegmentSize)
            {
                stream = GetStream();
                stream.Advance(ReserveForPrefix);

                stream.WriteByte(Chunk);
                sqn = Interlocked.Increment(ref currentSeqNo);
                PrimitiveEncoder.WriteInt64(stream, sqn);

                stream.Write(buffer, offset, MaxSegmentSize);
                offset += MaxSegmentSize;
                count -= MaxSegmentSize;
                SendQueue.Enqueue(GetPackage(stream, sqn, this));

            }
            if (count > 0)
            {
                stream = GetStream();
                stream.Advance(ReserveForPrefix);

                stream.WriteByte(Chunk);
                sqn = Interlocked.Increment(ref currentSeqNo);
                PrimitiveEncoder.WriteInt64(stream, sqn);

                stream.Write(buffer, offset, count);
                SendQueue.Enqueue(GetPackage(stream, sqn, this));
            }
        }

        private void FrameChunkEnqueue(in Segment first, in Segment second)
        {
            int count = first.Count + second.Count;
            int firstOffset = 0;
            int secondOffset = 0;

            var stream = GetStream();
            stream.Advance(ReserveForPrefix);

            stream.WriteByte(Head);
            long sqn = Interlocked.Increment(ref currentSeqNo);
            PrimitiveEncoder.WriteInt64(stream, sqn);
            PrimitiveEncoder.WriteInt32(stream, count);

            if (first.Count < MaxSegmentSize)
            {
                stream.Write(first.Array, first.Offset, first.Count);
                count -= first.Count;
                firstOffset += first.Count;

                if (second.Count > 0)
                {
                    int cnt = Math.Min(MaxSegmentSize - first.Count, second.Count);
                    stream.Write(second.Array, second.Offset, cnt);
                    count -= cnt;
                    secondOffset += cnt;
                }
            }
            else
            {
                stream.Write(first.Array, 0, MaxSegmentSize);
                firstOffset += MaxSegmentSize;
                count -= MaxSegmentSize;

            }
            SendQueue.Enqueue(GetPackage(stream, sqn, this));
            while (count > MaxSegmentSize)
            {
                stream = GetStream();
                stream.Advance(ReserveForPrefix);

                stream.WriteByte(Chunk);
                sqn = Interlocked.Increment(ref currentSeqNo);
                PrimitiveEncoder.WriteInt64(stream, sqn);

                int amountWritten = 0;
                if (firstOffset != first.Count)
                {
                    //int cnt = first.Count - firstOffset;
                    int cnt = Math.Min(MaxSegmentSize, first.Count - firstOffset);

                    stream.Write(first.Array, firstOffset + first.Offset, cnt);
                    count -= cnt;
                    firstOffset += cnt;
                    amountWritten += cnt;
                }
                int cc = MaxSegmentSize - amountWritten;// if 0  nothing happens.
                stream.Write(second.Array, second.Offset + secondOffset, cc);
                secondOffset += cc;
                count -= cc;
                SendQueue.Enqueue(GetPackage(stream, sqn, this));

            }
            if (count > 0)
            {
                stream = GetStream();
                stream.Advance(ReserveForPrefix);

                stream.WriteByte(Chunk);
                sqn = Interlocked.Increment(ref currentSeqNo);
                PrimitiveEncoder.WriteInt64(stream, sqn);

                stream.Write(second.Array, second.Offset + secondOffset, count);
                SendQueue.Enqueue(GetPackage(stream, sqn, this));
            }
        }

        #endregion

        #region Feedback
        public void HandleFeedback(byte[] buffer, int offset)
        {
            switch (buffer[offset])
            {
                case Ack:
                    AckReceived(buffer, offset);
                    break;
                case AckList:
                    AckListReceived(buffer, offset);
                    break;
                case Nack:
                    NackReceived(buffer, offset);
                    break;
                case ProbeResult:
                    ProbeResultReceived(buffer, offset);
                    break;
                case SynAck:
                    HandleSyc(buffer, offset);
                    break;
            }

        }


        private void AckReceived(byte[] buffer, int offset)
        {
            offset++;
            var seqNo = PrimitiveEncoder.ReadInt64(buffer, ref offset);
            // Console.WriteLine("Ack Received" + seqNo);

            bool act = false;
            lock (reSenderLock)
            {
                if (pendingPackages.TryRemove(seqNo, out var p))
                {
                    act = true;

                    p.complete = true;
                    Interlocked.Add(ref PendingBytes, -p.Stream.Position32);
                    Timer.Remove(p);

                    duplicateAckCounter.TryRemove(seqNo, out _);
                    if (p.resendMultplier == 1)
                    {
                        var deltaT = GetTime() - p.CreationTime;
                        CalculateRTT(deltaT);
                    }

                    ReturnStream(p.Stream);
                    p.Release();
                    ReturnPackage(p);

                }
            }
            if (act)
            {
                StartProducer();
                ExpandWindow();
            }
        }

        private void HandleSyc(byte[] buffer, int offset)
        {
            offset++;
            var receiverCurrSeq = PrimitiveEncoder.ReadInt64(buffer, ref offset);
            estimatedBandwidth = PrimitiveEncoder.ReadInt64(buffer, ref offset);
            bool any = false;
            lock (reSenderLock)
            {
                foreach (var item in pendingPackages)
                {
                    if (item.Key >= receiverCurrSeq)
                        continue;

                    var seqNo = item.Key;
                    if (pendingPackages.TryRemove(seqNo, out Package p))
                    {
#if EnableDebugLogs
                        Console.WriteLine(">>>>>>>>>>> Trimmed <<<<<<<<<<<<");
#endif
                        Timer.Remove(p);

                        duplicateAckCounter.TryRemove(seqNo, out _);
                        Interlocked.Add(ref PendingBytes, -p.Stream.Position32);

                        TrimWindow();
                        ReturnStream(p.Stream);
                        ReturnPackage(p);

                        p.Release();
                        any = true;
                    }

                }
            }
            if (any)
            {
                StartProducer();
                RTO = Math.Min(RTO * 2, MaxRTO);

            }
        }

        ConcurrentDictionary<long, int> duplicateAckCounter = new ConcurrentDictionary<long, int>();
        private void NackReceived(byte[] buffer, int offset)
        {
            offset++;
            long fromSeq = PrimitiveEncoder.ReadInt64(buffer, ref offset);
            long toSeq = PrimitiveEncoder.ReadInt64(buffer, ref offset);
            int gap = (int)(toSeq - fromSeq);
            List<long> gapSeqs = new List<long>();
            for (int i = 0; i < gap; i++)
            {
                gapSeqs.Add(++fromSeq);
            }
            bool act = false;
            lock (reSenderLock)
            {
                foreach (var seqNo in gapSeqs)
                {
                    if (pendingPackages.TryGetValue(seqNo, out Package p))
                    {
                        duplicateAckCounter.TryAdd(seqNo, 0);
                        TrimWindow();
#if EnableDebugLogs
                        Console.WriteLine(">>>>>>     early detect trimmed");
#endif

                        if (++duplicateAckCounter[seqNo] == 3)
                        {
                            p.complete = true;
#if EnableDebugLogs
                            Console.WriteLine(" ############## Duplicate ACK");
#endif
                            TrimWindow();

                            RTO = Math.Min(RTO * 2, MaxRTO);
                            PriorityQueue.Enqueue(p);
                            act = true;

                        }

                    }

                }
            }
            if (act)
            {
                StartProducer();
            }
        }

        internal void PacketTimedOut(Package package)
        {
            bool act = false;
            lock (reSenderLock)
            {
                if (pendingPackages.TryGetValue(package.SeqNo, out var p))
                {
                    PriorityQueue.Enqueue(p);
                    act = true;
                }
            }
            if (act)
            {
                //Console.WriteLine("Loss " + p.SeqNo);
                ShrinkWindow();
                UpdateRtoExponentialBackoff();
                StartProducer();

            }

        }
        #endregion

        #region Flow Control
        private void CalculateRTT(long deltaT)
        {
            const float alpha = 0.875f;
            const float alpha_ = 1 - alpha;
            const float Ro = 0.25f;
            const float Ro_ = 1 - Ro;

            float rttCurrent = deltaT;
            Mdev = Ro_ * Mdev + Ro * Math.Abs(rttCurrent - RTT);
            RTT = alpha * RTT + alpha_ * rttCurrent;
            RTO = Math.Min(RTT + 4 * Mdev, MaxRTO);
            // Console.WriteLine($"- - Good - -  Mdev: {Mdev}  //  Rtt {RTT}   //  RTO {RTO} // Windowsize : {WindowSize.ToString("N0")}");


        }
        private void UpdateRtoExponentialBackoff()
        {
            RTO = Math.Min(RTO * 2, MaxRTO);
#if EnableDebugLogs
            Console.WriteLine($"- - Loss - -  Mdev: {Mdev}  //  Rtt {RTT}   //" +
                $"  RTO {RTO} // Windowsize : {WindowSize.ToString("N0")}");
#endif
        }
        Spinlock updateLocker = new Spinlock();

        long totalAcks = 0;
        int consecutiveGoodAcks = 1;
        int windowBumpTreshold = 50;
        private void ExpandWindow()
        {
            if (Interlocked.CompareExchange(ref WindowSize, 0, 0) < MaxWindowSize)
            {
                updateLocker.Take();
                Interlocked.Add(ref WindowSize, 10 + (int)(WindowIncrementMultiplier * MaxSegmentSize / Interlocked.Increment(ref totalAcks)));
                //Interlocked.Add(ref WindowSize, 10 + (int)(1500) );
                if (EnableWindowBump)
                {
                    consecutiveGoodAcks++;
                    if (consecutiveGoodAcks > windowBumpTreshold)
                    {
                        consecutiveGoodAcks = 0;
                        totalAcks = 1;
                        windowBumpTreshold += windowBumpTreshold;
#if EnableDebugLogs
                        Console.WriteLine("..................... >>>>>> Smoothed");
#endif
                    }
                }

                updateLocker.Release();
            }
        }

        private void TrimWindow()
        {
            updateLocker.Take();

            Interlocked.Exchange(ref WindowSize, (int)(WindowSize * SoftwindowTrim));
            // Interlocked.Exchange(ref conseq, (int)(conseq*0.87f));
            totalAcks = 4;
            consecutiveGoodAcks = 0;
            windowBumpTreshold = Math.Max(WindowBumpLowerTreshold, windowBumpTreshold - WindowBumpLowerTreshold);

            // wast with max lol
            if (WindowSize < MinWindowSize)
            {
                totalAcks = 0;
                consecutiveGoodAcks = 0;
                windowBumpTreshold = WindowBumpLowerTreshold;
            }

            updateLocker.Release();
        }

        private void ShrinkWindow()
        {
            updateLocker.Take();

            Interlocked.Exchange(ref WindowSize, (int)(WindowIncrementMultiplier * WindowSize * TimeoutWindowTrim));
            totalAcks = 4;
            consecutiveGoodAcks = 0;
            windowBumpTreshold = Math.Max(WindowBumpLowerTreshold, windowBumpTreshold - WindowBumpLowerTreshold);

            if (WindowSize < MinWindowSize)
            {
                totalAcks = 0;
                consecutiveGoodAcks = 0;
                windowBumpTreshold = WindowBumpLowerTreshold;
            }

            updateLocker.Release();
        }

        #endregion

        #region Util

        internal long GetTime()
        {
            return stopwatch.ElapsedMilliseconds;
        }

        public void ShutDown()
        {
            Interlocked.Exchange(ref shutdownSignalled, 1);
            lock (reSenderLock)
            {
                while (SendQueue.TryDequeue(out var segment))
                {
                    //Timer.Remove(segment);
                    segment.Release();
                    ReturnStream(segment.Stream);
                }
                while (PriorityQueue.TryDequeue(out var segment))
                {
                    Timer.Remove(segment);
                    segment.Release();
                    ReturnStream(segment.Stream);

                }
                foreach (var item in pendingPackages)
                {
                    item.Value.Release();
                    Timer.Remove(item.Value);
                }
                pendingPackages.Clear();
            }

        }
        #endregion

        #region Pooling
        static ConcurrentQueue<Package> packagePool = new ConcurrentQueue<Package>();
        internal Package GetPackage(PooledMemoryStream stream, long sqn, SenderModule senderModule)
        {
            if (!packagePool.TryDequeue(out var package))
            {
                package = new Package();
            }
            package.Set(stream, sqn, senderModule);
            return package;

        }
        internal void ReturnPackage(Package p) => packagePool.Enqueue(p);

        static SharerdMemoryStreamPool pool = new SharerdMemoryStreamPool();
        static ConcurrentQueue<PooledMemoryStream> strmPool = new ConcurrentQueue<PooledMemoryStream>();
        internal static PooledMemoryStream GetStream()
        {
            //return pool.RentStream();
            if (!strmPool.TryDequeue(out PooledMemoryStream package))
            {
                package = new PooledMemoryStream();
            }
            return package;

        }
        internal static void ReturnStream(PooledMemoryStream p) /*=> pool.ReturnStream(p);*/{ p.Position32 = 0; strmPool.Enqueue(p); }

        #endregion

        #region Experimental

        #region Probing(not that kind)
        //if (GetTime()- lastProbeTime>1000)
        //{
        //    SendProbes();

        //}
        double probeDt = 0;
        long lastProbeTime = 0;
        private void SendProbes()
        {
            lastProbeTime = GetTime();
            var Stream = GetStream();
            Stream.WriteByte(Probe1);
            //Stream.Position32 = 64000;

            SendRequested?.Invoke(Stream.GetBuffer(), 0, Stream.Position32);
            var t1 = stopwatch.Elapsed.TotalMilliseconds * 1000000;

            Stream.Position32 = 0;
            Stream.WriteByte(Probe2);
            Stream.Position32 = 64000;

            SendRequested?.Invoke(Stream.GetBuffer(), 0, Stream.Position32);
            var t2 = stopwatch.Elapsed.TotalMilliseconds * 1000000;
            probeDt = t2 - t1;
            Console.WriteLine("Local Dt " + probeDt);

        }
        double estimatedBandwidth = 0;
        private void ProbeResultReceived(byte[] buffer, int offset)
        {
            offset++;
            var dt = PrimitiveEncoder.ReadDouble(buffer, ref offset);
            double delta = Math.Abs(dt);
            double seconds = dt / 1000000000L;
            estimatedBandwidth =/* 0.875 * estimatedBandwidth + (1 - 0.875) * */64000 * (1d / seconds);
            Console.WriteLine("###################     Bandwidth : " + estimatedBandwidth.ToString("N0"));
            Console.WriteLine("###################     dt : " + delta.ToString("N0"));
        }
        #endregion

        private void AckListReceived(byte[] buffer, int offset)
        {
            offset++;
            var currSeq = PrimitiveEncoder.ReadInt64(buffer, ref offset);
            int count = PrimitiveEncoder.ReadInt32(buffer, ref offset);

            long[] acks = new long[count];
            for (int i = 0; i < count; i++)
            {
                var sq = PrimitiveEncoder.ReadInt64(buffer, ref offset);
                acks[i] = sq;
            }
            bool act = false;
            Package p = null;
            lock (reSenderLock)
            {
                for (int i = 0; i < acks.Length; i++)
                {
                    var seqNo = acks[i];
                    if (pendingPackages.TryRemove(seqNo, out p))
                    {
                        act = true;
                        p.complete = true;
                        Timer.Remove(p);
#if EnableDebugLogs
                        Console.WriteLine("ACK " + seqNo);
#endif
                        duplicateAckCounter.TryRemove(seqNo, out _);
                        Interlocked.Add(ref PendingBytes, -p.Stream.Position32);

                        if (p.resendMultplier == 1)
                        {
                            var deltaT = GetTime() - p.CreationTime;

                            CalculateRTT(deltaT);
                            ExpandWindow();
                        }
                        else
                        {
                            RTO = RTO * 2;
                        }

                        ReturnStream(p.Stream);
                        p.Release();
                        ReturnPackage(p);

                    }
                }


            }
            if (act)
            {
                StartProducer();

            }

        }
        #endregion

    }
}
