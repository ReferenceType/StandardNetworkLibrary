using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetworkLibrary.Utils;
using NetworkLibrary.P2P;

namespace NetworkLibrary.DistributedP2P.Components
{
    internal class TimeSync
    {

        private Stopwatch clientClock = Stopwatch.StartNew();
        private double timeOffset;
        private TimeSpan timeOffsetd;
        private long syncCount = 0;

        private List<double> timesHistory = new List<double>();
        private List<TimeSpan> timesHistoryd = new List<TimeSpan>();
        private readonly SemaphoreSlim asyncLock = new SemaphoreSlim(1, 1);

        IDistributedConnection connection;

        public TimeSync(IDistributedConnection connection)
        {
            this.connection = connection;
        }

        AsyncDispatcher timesyncOperation;
        public void StartAutoTimeSync(int periodMs, bool usePTP = false)
        {
            Interlocked.Exchange(ref timesyncOperation, new AsyncDispatcher())?.Abort();
            int failureCount = 0;
            timesyncOperation.LoopPeriodicTask(async () =>
            {
                try
                {
                    
                    bool result = await SyncTime(usePTP).ConfigureAwait(false);
                    if ( result == false)
                    {
                        if (++failureCount > 2)
                        {
                            StopAutoTimeSync();
                        }

                    }
                    else
                    {
                        failureCount = 0;
                    }
                   
                }
                catch (Exception e)
                {
                    StopAutoTimeSync();
                }

            }, periodMs).ConfigureAwait(false);
        }

        public void StopAutoTimeSync()
        {
            timesyncOperation?.Abort();
            ClearData();
        }

        private async void ClearData()
        {
            try
            {
                await asyncLock.WaitAsync().ConfigureAwait(false);
                //not sure
                syncCount = 0;
                timesHistory.Clear();
                timesHistoryd.Clear();
            }
            catch { }
            finally 
            { 
                asyncLock.Release();
            }
            
        }

        public async Task<bool> SyncTime(bool usePtp = false)
        {
            try
            {
                await asyncLock.WaitAsync().ConfigureAwait(false);

                //12 first, 3,2,1

                int sampleSize = 12;
                var sCnt = Interlocked.CompareExchange(ref syncCount, 0, 0);

                if (sCnt > 50)
                    sampleSize = 4;

               var localHistory = new List<TimeResult>();
               var localHistoryd = new List<TimeResult>();
                for (int i = 0; i < sampleSize; i++)
                {
                    var result = usePtp ? await GetOffsetPTP().ConfigureAwait(false) : await GetOffsetNTP().ConfigureAwait(false);
                    if (result.Succes)
                    {
                        localHistory.Add(result);
                        localHistoryd.Add(result);
                    }
                    else return false;
                }

                var bestSamples = localHistory.OrderBy(x => x.RTT).Take((sampleSize / 2)).Select(x => x.PreciseTime).ToList();
                var bestSamplesd = localHistoryd.OrderBy(x => x.RTT).Take((sampleSize / 2)).Select(x=>x.DateTimeOffset).ToList();
                //var bestSamples = localHistory;
                //var bestSamplesd = localHistoryd;

                timesHistory.AddRange(bestSamples);
                timesHistoryd.AddRange(bestSamplesd);
                

                if (timesHistory.Count < 4)
                    return false;

                var times = Statistics.FilterOutliers(timesHistory);
                var timesd = Statistics.FilterOutliers(timesHistoryd);

                if (timesHistory.Count > 100)
                {
                    timesHistory = timesHistory.Skip(10).ToList();
                    timesHistoryd = timesHistoryd.Skip(10).ToList();
                }

                double average = times.Sum() / times.Count();
                double averaged = timesd.Sum(ts => ts.Ticks) / timesd.Count();
                timeOffset = average;

                if (sCnt > 0)
                {
                    var calculatedAvg = TimeSpan.FromTicks((long)averaged);
                    if (timeOffsetd < calculatedAvg)
                    {
                        timeOffsetd = calculatedAvg;
                    }
                }
                else
                {
                    timeOffsetd = TimeSpan.FromTicks((long)averaged);
                }

                Interlocked.Increment(ref syncCount);
                return true;

            }
            finally
            {
                asyncLock.Release();
            }

        }

        class TimeResult { public double PreciseTime; public bool Succes; public DateTime ServerUTC; public TimeSpan DateTimeOffset;  public double RTT; }

        private async Task<TimeResult> GetOffsetNTP()
        {
            var msg = new MessageEnvelope()
            {
                Header = Constants.TimeSync,
                IsInternal = true,
            };
            var now = clientClock.Elapsed.TotalMilliseconds;
            var nowd = DateTime.UtcNow;

            var response = await connection.SendMessageAndWaitResponse(msg).ConfigureAwait(false);
            if (response.Header != MessageEnvelope.RequestTimeout)
            {
                var now1 = clientClock.Elapsed.TotalMilliseconds;
                var now1d = DateTime.UtcNow;

                var serverTime = PrimitiveEncoder.ReadFixedDouble(response.Payload, response.PayloadOffset);
                var serverTimed = response.TimeStamp;

                var timeOffset = ((serverTime - now) + (serverTime - now1)) / 2;
                var timeOffsetd = ((serverTimed - nowd) + (serverTimed - now1d)).TotalMilliseconds / 2;
                TimeSpan offd = TimeSpan.FromMilliseconds(timeOffsetd);

                return new TimeResult() { PreciseTime = timeOffset, DateTimeOffset = offd, Succes = true, RTT = now1-now };
            }
            return new TimeResult();

        }

        private async Task<TimeResult> GetOffsetPTP()
        {

            var t1 = await GetServerTime();
            if (t1.Succes)
            {
                var t2 = clientClock.Elapsed.TotalMilliseconds;
                var t2d = DateTime.UtcNow;

                var t3 = t2;
                var t3d = t2d;

                var t4 = await GetServerTime();
                var trtt = clientClock.Elapsed.TotalMilliseconds;

                if (t4.Succes)
                {
                    double offset = (((t4.PreciseTime - t3) - (t2 - t1.PreciseTime)) / 2);
                    double offsetd = (((t4.ServerUTC - t3d) - (t2d - t1.ServerUTC)).TotalMilliseconds / 2);
                    TimeSpan offd = TimeSpan.FromMilliseconds(offsetd);
                    return new TimeResult() { PreciseTime = offset, DateTimeOffset = offd, Succes = true,RTT = t2- trtt };
                }
                else
                    return new TimeResult();
            }
            else
                return new TimeResult();

        }

        private async Task<TimeResult> GetServerTime()
        {
            var msg = new MessageEnvelope()
            {
                Header = Constants.TimeSync,
                IsInternal = true,
            };
            var response = await connection.SendMessageAndWaitResponse(msg);
            if (response.Header != MessageEnvelope.RequestTimeout)
            {
                var serverTime = PrimitiveEncoder.ReadFixedDouble(response.Payload, response.PayloadOffset);
                return new TimeResult() { PreciseTime = serverTime, ServerUTC = response.TimeStamp, Succes = true };
            }
            return new TimeResult();

        }

        public double GetTime()
        {
            return clientClock.Elapsed.TotalMilliseconds + timeOffset;
        }

        public DateTime GetDateTime()
        {
            return DateTime.UtcNow.Add(timeOffsetd);
        }
    }
}
