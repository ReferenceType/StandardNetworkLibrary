using NetworkLibrary.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;

namespace NetworkLibrary
{
    /*
     * This behaves like ArrayPool<>. The butckets are TLS due to concurrent bag. ( ThreadLocal<ThreadLocalList> m_locals )
     */
    public class BufferPool
    {
        public static bool ForceGCOnCleanup = true;
        public const int MaxBufferSize = 1073741824;
        public const int MinBufferSize = 256;

        private static readonly ConcurrentBag<byte[]>[] bufferBuckets =  new ConcurrentBag<byte[]>[32];
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

       
        static BufferPool()
        {
            Init();
            Thread thread = new Thread(MaintainMemory);
            thread.Priority= ThreadPriority.Lowest;
            thread.Start();
        }

        // creates bufferBuckets structure
        private static void Init()
        {
            //bufferBuckets = new ConcurrentDictionary<int, ConcurrentBag<byte[]>>();
            for (int i = 8; i < 31; i++)
            {
                bufferBuckets[i] = new ConcurrentBag<byte[]>();
            }
        }

        private static void MaintainMemory()
        {
            // Check each bucket periodically if the free capacity limit is exceeded
            // Dump excess amount
            while (true)
            {
                Thread.Sleep(10000);
                try
                {
                    for (int k = 0; k < bufferBuckets.Length; k++)
                    {
                        var size = GetBucketSize(k);
                        var bag = bufferBuckets[k];
                        if (bag == null) continue;

                        if (bucketCapacityLimits[size] < bag.Count)
                        {
                            // check 5 times slowly to make sure buffer bucket is not hot.
                            for (int i = 0; i < 5; i++)
                            {
                                Thread.Sleep(1);
                                if (bucketCapacityLimits[size] >= bag.Count) continue;
                            }

                            // trim slowly
                            while (bag.Count > bucketCapacityLimits[size])
                            {
                                bag.TryTake(out var buffer);
                                buffer = null;
                                Thread.Sleep(20);

                            }
                            if (ForceGCOnCleanup)
                                GC.Collect();
                        }
                    }
                }
                catch(Exception e)
                {
                    MiniLogger.Log(MiniLogger.LogLevel.Error,"Buffer manager encountered an error: " + e.Message);
                }

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
            if(MaxBufferSize < size)
                throw new InvalidOperationException(string.Format("Unable to rent buffer bigger than max buffer size: {0}",MaxBufferSize));

            int idx = GetBucketIndex(size);
            if (!bufferBuckets[idx].TryTake(out byte[] buffer))
            {
                buffer = new byte[GetBucketSize(idx)];
            }

            return buffer;
        }

        /// <summary>
        /// Return rented buffer. Take care not to return twice!
        /// </summary>
        /// <param name="buffer"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReturnBuffer(byte[] buffer)
        {

            if (MinBufferSize > buffer.Length)
                throw new InvalidOperationException(string.Format("Unable to return, buffer smaller than min buffer size: {0}", MinBufferSize));

            int idx = GetBucketIndex(buffer.Length);
            bufferBuckets[idx-1].Add(buffer);
        }

        /// <summary>
        /// Sets Bucket size
        /// </summary>
        /// <param name="bucketNumber"></param>
        /// <param name="amount"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public static void SetBucketLimit(int bucketNumber, int amount)
        {
            if (bucketNumber >= 31 || bucketNumber < 8)
                throw new InvalidOperationException("Bucket number needs to be between 8 and 31 (inclusive)");

            int size = GetBucketSize(bucketNumber);
            bucketCapacityLimits[size] = amount;
        }

        /// <summary>
        /// Sets Bucket size
        /// </summary>
        /// <param name="bucketSize"></param>
        /// <param name="amount"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public static void SetBucketLimitBySize(int bucketSize, int amount)
        {
            if (bucketSize > 1073741824 || bucketSize <= 256)
                throw new InvalidOperationException("Bucket number needs to be between 8 and 31 (inclusive)");

            if (bucketCapacityLimits.ContainsKey(bucketSize))
                bucketCapacityLimits[bucketSize] = amount;

            int idx = GetBucketIndex(bucketSize);
            int size = GetBucketSize(idx);
            bucketCapacityLimits[size] = amount;
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
