using NetworkLibrary.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;

namespace NetworkLibrary
{
    /*
     * Concurrent bag has Tls list ( ThreadLocal<ThreadLocalList> m_locals )
     * each bucket holds a set of weak references to byte arrays
     * this arrays are pooled and resuable and we preserve the peak memory usage by this.
     * If application calls the GC gen2 collect some of this weak references are cleared,
     * this way we trim the pools automatically if they are not referenced by the application.
     * 
     * you can also configure the pool to auto GC collect(also does gen2) if the application is mostly idle and 
     * we reached to some threshold on workingset memory.
     */
    public class BufferPool
    {
        public static bool ForceGCOnCleanup = true;
        public static int MaxMemoryBeforeForceGc = 100000000;
        public const int MaxBufferSize = 1073741824;
        public const int MinBufferSize = 256;
        private static readonly ConcurrentBag<WeakReference<byte[]>> weakReferencePool= new ConcurrentBag<WeakReference<byte[]>>();
        private static readonly ConcurrentBag<WeakReference<byte[]>>[] bufferBuckets =  new ConcurrentBag<WeakReference<byte[]>>[32];
        private static SortedDictionary<int, int> bucketCapacityLimits = new SortedDictionary<int, int>()
        {
            { 256,10000 },
            { 512,10000 },
            { 1024,10000 },
            { 2048,5000 },
            { 4096,1000 },
            { 8192,1000 },
            { 16384,500 },
            { 32768,300 },
            { 65536,300 },
            { 131072,200 },
            { 262144,50 },
            { 524288,10 },
            { 1048576,4 },
            { 2097152,2 },
            { 4194304,1 },
            { 8388608,1 },
            { 16777216,1 },
            { 33554432,0 },
            { 67108864,0 },
            { 134217728,0 },
            { 268435456,0 },
            { 536870912,0 },
            { 1073741824,0 }

        };
        static readonly Process process = Process.GetCurrentProcess();
        static ManualResetEvent autoGcHandle =  new ManualResetEvent(false);

        static BufferPool()
        {
            Init();
            Thread thread = new Thread(MaintainMemory);
            thread.Priority= ThreadPriority.Lowest;
            thread.Start();
        }

        /// <summary>
        /// Starts a task where GC.Collect() is called 
        /// if application consumed less than %1 proccessor time and memory is above threashold
        /// </summary>
        public static void StartCollectGcOnIdle()
        {
            autoGcHandle.Set();
        }

        /// <summary>
        /// Stops a task where GC.Collect() is called 
        /// if application consumed less than %1 proccessor time and memory is above threashold
        /// </summary>
        public static void StopCollectGcOnIdle()
        {
            autoGcHandle.Reset();
        }

        // creates bufferBuckets structure
        private static void Init()
        {
            //bufferBuckets = new ConcurrentDictionary<int, ConcurrentBag<byte[]>>();
            for (int i = 8; i < 31; i++)
            {
                bufferBuckets[i] = new ConcurrentBag<WeakReference<byte[]>>();
            }
        }

        private static void MaintainMemory()
        {
            var lastTime = process.TotalProcessorTime;
            while (true)
            {
                autoGcHandle.WaitOne();
                Thread.Sleep(10000);
                var currentProcTime = process.TotalProcessorTime;
                var deltaT = (lastTime - currentProcTime).TotalMilliseconds;
                lastTime = currentProcTime;

                if (deltaT <100 && process.WorkingSet64< MaxMemoryBeforeForceGc)
                    GC.Collect();
                process.Refresh();
            }
           
        }


        /// <summary>
        /// Rents a buffer at least the the size requested
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] RentBuffer(int size)
        {
            byte[] buffer;
            if(MaxBufferSize < size)
                throw new InvalidOperationException(
                    string.Format("Unable to rent buffer bigger than max buffer size: {0}",MaxBufferSize));
            if (size <= MinBufferSize) return new byte[size];

            int idx = GetBucketIndex(size);

            while(bufferBuckets[idx].TryTake(out WeakReference<byte[]> bufferRef))
            {
                if (bufferRef.TryGetTarget(out buffer))
                {
                    weakReferencePool.Add(bufferRef);
                    return buffer;
                }
            }
            buffer = new byte[GetBucketSize(idx)];
           return buffer;

        }

        /// <summary>
        /// Return rented buffer. Take care not to return twice!
        /// </summary>
        /// <param name="buffer"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReturnBuffer(byte[] buffer)
        {
            if (buffer.Length <= MinBufferSize) return;

            int idx = GetBucketIndex(buffer.Length);
            if(weakReferencePool.TryTake(out var wr))
            {
                wr.SetTarget(buffer);
                bufferBuckets[idx - 1].Add(wr);

            }
            else
            bufferBuckets[idx-1].Add(new WeakReference<byte[]>(buffer));
            buffer = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetBucketSize(int bucketIndex)
        {
            return 1 << bucketIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetBucketIndex(int size)
        {
            return 32 - LeadingZeros(size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int LeadingZeros(int x)
        {
            const int numIntBits = sizeof(int) * 8;
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;
            //count the ones
            x -= x >> 1 & 0x55555555;
            x = (x >> 2 & 0x33333333) + (x & 0x33333333);
            x = (x >> 4) + x & 0x0f0f0f0f;
            x += x >> 8;
            x += x >> 16;
            return numIntBits - (x & 0x0000003f); //subtract # of 1s from 32
        }
    }
}
