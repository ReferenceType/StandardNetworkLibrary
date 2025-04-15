using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetworkLibrary.Utils;
using NetworkLibrary.P2P;
using static NetworkLibrary.DistributedP2P.Components.TimeSync;
using NetworkLibrary.P2P.Components.HolePunch;

namespace NetworkLibrary.DistributedP2P.Components
{
    internal class TimeSync:IDisposable
    {

        private Stopwatch clientClock = Stopwatch.StartNew();
        private double timeOffset;
        private TimeSpan timeOffsetd;
        private NTPClient client;
        private readonly SemaphoreSlim asyncLock = new SemaphoreSlim(1, 1);

        IDistributedConnection connection;

        TimeSyncer timeSync = new TimeSyncer();
        bool cancel = false;
        int maxPeriod = 30000;
        Queue<double> offsetHistory = new Queue<double>();
        public TimeSync(IDistributedConnection connection)
        {
            this.connection = connection;
           
        }

        public void SetEndpoint(EndpointData endpointData)
        {
            string ip = IPHelper.Byte2Sting(endpointData.Ip);
            var cl = new NTPClient(ip, endpointData.Port, clientClock);
            Interlocked.Exchange(ref client, cl)?.Dispose();
        }

        public async void StartAutoTimeSync()
        {

            int failureCount = 0;
            cancel=false;
            int PeriodLocal = 2000;
            while (!cancel) 
            {
                try
                {
                    await Task.Delay(PeriodLocal);
                    if (cancel)
                        return;
                    
                    bool result = await SyncTime().ConfigureAwait(false);
                    if (result == false)
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

                    PeriodLocal = Math.Min(PeriodLocal * 2, maxPeriod);

                }
                catch (Exception e)
                {
                    StopAutoTimeSync();
                    return;
                }
            }
        }

        public void StopAutoTimeSync()
        {
            Console.WriteLine("SyncStopped");
            cancel = true;
            ClearData();
        }

        private async void ClearData()
        {
            try
            {
                await asyncLock.WaitAsync().ConfigureAwait(false);
                offsetHistory.Clear();
            }
            catch { }
            finally 
            { 
                asyncLock.Release();
            }
            
        }

        public async Task<bool> SyncTime()
        {
            try
            {
                await asyncLock.WaitAsync().ConfigureAwait(false);


                int sampleSize = 12;

                var localHistory = new List<TimeResult>();
                while(localHistory.Count <sampleSize)
                {
                    var result = await GetOffsetNTPUdp().ConfigureAwait(false);
                    if (result.Succes)
                    {
                        localHistory.Add(result);
                    }
                }


                var filteredSample = timeSync.GetBestTimeOffset(localHistory, sampleSize);

                offsetHistory.Enqueue(filteredSample.PreciseTime);
                if (offsetHistory.Count >= 4) 
                {
                    var result= Statistics.FilterOutliers(offsetHistory);
                    timeOffset = result.Average();
                }
                else
                {
                    timeOffset = offsetHistory.Average();
                }

                if (offsetHistory.Count>5)
                    offsetHistory.Dequeue();

                if(filteredSample.DateTimeOffset > timeOffsetd)
                    timeOffsetd  = filteredSample.DateTimeOffset;
                return true;

            }
            finally
            {
                asyncLock.Release();
            }

        }

        private Task<TimeResult> GetOffsetNTPUdp()
        {
            return client.GetServerTime(2000);
        }


       


        private async Task<TimeResult> GetOffsetNTPTcp()
        {
            var msg = new MessageEnvelope()
            {
                Header = Constants.TimeSync,
                IsInternal = true,
            };

            var t1 = clientClock.Elapsed.TotalMilliseconds;
            var t1d = DateTime.UtcNow;

            var response = await connection.SendMessageAndWaitResponse(msg).ConfigureAwait(false);

            var t4 = clientClock.Elapsed.TotalMilliseconds;
            var t4d = DateTime.UtcNow;

            if (response.Header != MessageEnvelope.RequestTimeout)
            {
                var t2 = PrimitiveEncoder.ReadFixedDouble(response.Payload, response.PayloadOffset);
                var t3 = t2 + 0.005;

                var t2d = response.TimeStamp;
                var t3d = response.TimeStamp;

                // Calculate delay and offset using the full NTP formula
                var delay = (t4 - t1) - (t3 - t2);
                var offset = ((t2 - t1) + (t3 - t4)) / 2;

                var delayd = ((t4d - t1d) - (t3d - t2d)).TotalMilliseconds;
                var offsetd = ((t2d - t1d) + (t3d - t4d)).TotalMilliseconds / 2;

                TimeSpan offd = TimeSpan.FromMilliseconds(offsetd);

                return new TimeResult()
                {
                    PreciseTime = offset,
                    DateTimeOffset = offd,
                    Succes = true,
                    RTT = t4 - t1,
                    Delay = delay,
                };
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

        public void Dispose()
        {
           client.Dispose();
        }
    }

    internal class TimeSyncer
    {
        public List<TimeResult> timeHistory = new List<TimeResult>();

        private TimeResult GetMinRtt(List<TimeResult> timeHistory)
        {
            double min = int.MaxValue;
            TimeResult result = null;
            foreach (var item in timeHistory)
            {
                if (item.RTT < min)
                {
                    result = item;
                    min = item.RTT;
                }
            }
            return result;
        }

       
        public TimeResult GetBestTimeOffset(List<TimeResult> samples, int sampleSize)
        {
            if (samples.Count < 4) return GetMinRtt(samples);

            //var lowRttSamples = samples.OrderBy(x => x.RTT).Take(sampleSize / 2);

            var lowRttSamples = GetEnhancedFilteredSamples(samples, sampleSize / 2);


            // Calculate the median delay
            var medianDelay = GetMedian(lowRttSamples.Select(s => s.Delay));

            // Calculate asymmetry estimates for each sample
            foreach (var sample in lowRttSamples)
            {
                // Positive values indicate forward path > return path
                sample.AsymmetryEstimate = sample.Delay - medianDelay;
            }

            // Filter outliers based on asymmetry estimates
            var asymmetryValues = lowRttSamples.Select(s => s.AsymmetryEstimate);
            var filteredAsymmetry = Statistics.FilterOutliers(asymmetryValues);

            // Only keep samples with acceptable asymmetry estimates
            var finalSamples = lowRttSamples
                .Where(s => filteredAsymmetry.Contains(s.AsymmetryEstimate));
               

            // Calculate weighted average based on RTT (lower RTT = higher weight)
            double totalWeight = 0;
            double weightedSumPrecise = 0;
            double weightedSumDateTime = 0;

            foreach (var sample in finalSamples)
            {
                // Use inverse of RTT as weight
                double weight = 1.0 / sample.RTT;
                totalWeight += weight;
                weightedSumPrecise += weight * sample.PreciseTime;
                weightedSumDateTime += weight * sample.DateTimeOffset.TotalMilliseconds;
            }

            return new TimeResult
            {
                PreciseTime = weightedSumPrecise / totalWeight,
                DateTimeOffset = TimeSpan.FromMilliseconds(weightedSumDateTime / totalWeight),
                Succes = true,
                RTT = finalSamples.Average(s => s.RTT)
            };
        }

        private double GetMedian(IEnumerable<double> values)
        {
            var sortedValues = values.OrderBy(v => v).ToList();
            int count = sortedValues.Count;

            if (count % 2 == 0)
            {
                // Even count
                return (sortedValues[count / 2 - 1] + sortedValues[count / 2]) / 2;
            }
            else
            {
                // Odd count
                return sortedValues[count / 2];
            }
        }



        public IEnumerable<TimeResult> GetEnhancedFilteredSamples(List<TimeResult> samples, int desiredCount = 6)
        {
            if (samples.Count <= desiredCount) return samples;

            // Phase 1: Initial RTT filtering to eliminate extreme outliers
            var rttValues = samples.Select(s => s.RTT).ToList();
            var filteredRtt = Statistics.FilterOutliers(rttValues).ToList();
            var initialFiltered = samples.Where(s => filteredRtt.Contains(s.RTT));

            if (initialFiltered.Count() <= desiredCount) return initialFiltered;

            // Phase 2: Calculate clustering of offset values
            var clusters = ClusterSamples(initialFiltered);

            // Phase 3: Select best cluster based on size and consistency
            var bestCluster = SelectBestCluster(clusters);

            // If best cluster has enough samples, use it
            if (bestCluster.Count >= desiredCount)
            {
                return bestCluster.OrderBy(s => s.RTT).Take(desiredCount);
            }

            // Otherwise, use hybrid approach combining RTT and consistency
            return RankSamplesByQuality(initialFiltered).Take(desiredCount);
        }

        // Cluster samples based on their offset values
        private List<List<TimeResult>> ClusterSamples(IEnumerable<TimeResult> samples)
        {
            // Simple clustering algorithm based on offset proximity
            const double clusterThreshold = 0.5; // ms - samples within 0.5ms of each other form a cluster

            var clusters = new List<List<TimeResult>>();
            var remaining = new List<TimeResult>(samples);

            while (remaining.Count > 0)
            {
                var currentSample = remaining[0];
                remaining.RemoveAt(0);

                var cluster = new List<TimeResult> { currentSample };

                // Find all samples close enough to this one to form a cluster
                for (int i = remaining.Count - 1; i >= 0; i--)
                {
                    if (Math.Abs(remaining[i].PreciseTime - currentSample.PreciseTime) <= clusterThreshold)
                    {
                        cluster.Add(remaining[i]);
                        remaining.RemoveAt(i);
                    }
                }

                clusters.Add(cluster);
            }

            return clusters;
        }

        // Select the best cluster based on size and internal consistency
        private List<TimeResult> SelectBestCluster(List<List<TimeResult>> clusters)
        {
            if (clusters.Count == 0) return new List<TimeResult>();
            if (clusters.Count == 1) return clusters[0];

            // Score each cluster
            var scoredClusters = clusters.Select(cluster => new
            {
                Cluster = cluster,
                // Size is important, but so is consistency of offset values
                Size = cluster.Count,
                Consistency = CalculateConsistency(cluster),
                AvgRTT = cluster.Average(s => s.RTT)
            });

            // Prioritize larger clusters with better consistency and lower RTT
            return scoredClusters
                .OrderByDescending(c => c.Size * 5 + c.Consistency * 3 - c.AvgRTT)
                .First()
                .Cluster;
        }

        // Calculate consistency score (lower is better)
        private double CalculateConsistency(List<TimeResult> cluster)
        {
            if (cluster.Count <= 1) return 0;

            var offsets = cluster.Select(s => s.PreciseTime);
            double avg = offsets.Average();
            double variance = offsets.Sum(o => Math.Pow(o - avg, 2)) / cluster.Count;

            // Lower variance means higher consistency
            return 1.0 / (1.0 + variance);
        }

        // Rank samples by multiple quality indicators
        private IEnumerable<TimeResult> RankSamplesByQuality(IEnumerable<TimeResult> samples)
        {
            int sampleCount = samples.Count();
            if (sampleCount <= 1) return samples;

            // Calculate average and standard deviation of all offset values
            var offsets = samples.Select(s => s.PreciseTime);
            double avgOffset = offsets.Average();
            double stdDev = Math.Sqrt(offsets.Sum(o => Math.Pow(o - avgOffset, 2)) / sampleCount);

            // Score each sample using multiple factors
            var scoredSamples = samples.Select(sample => new
            {
                Sample = sample,
                // How close to the average offset (lower is better)
                OffsetDeviation = Math.Abs(sample.PreciseTime - avgOffset) / stdDev,
                // RTT (lower is better)
                NormalizedRTT = sample.RTT / samples.Average(s => s.RTT),
                // If available, path asymmetry (lower is better)
                AsymmetryScore = sample.AsymmetryEstimate != 0 ?
                    Math.Abs(sample.AsymmetryEstimate) / samples.Average(s => Math.Abs(s.AsymmetryEstimate)) : 1
            });

            // Calculate composite score (lower is better)
            var rankedSamples = scoredSamples
                .Select(s => new
                {
                    s.Sample,
                    Score = s.OffsetDeviation * 2 + s.NormalizedRTT + s.AsymmetryScore
                })
                .OrderBy(s => s.Score)
                .Select(s => s.Sample);
                

            return rankedSamples;
        }

       

    }

}







