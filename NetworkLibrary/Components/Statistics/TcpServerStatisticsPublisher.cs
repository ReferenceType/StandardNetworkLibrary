using NetworkLibrary.TCP.Base;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

namespace NetworkLibrary.Components.Statistics
{
    public class TcpStatisticsStringData
    {
        public string PendingBytes { get; set; }
        public string CongestionLevel { get; set; }
        public string TotalBytesSent { get; set; }
        public string TotalBytesReceived { get; set; }
        public string TotalMessageDispatched { get; set; }

        public string SendRate { get; set; }
        public string ReceiveRate { get; set; }
        public string MessageDispatchRate { get; set; }
        public string TotalMessageReceived { get; internal set; }
    }

    public class TcpStatistics
    {
        TcpStatisticsStringData sessionStatsJson = new TcpStatisticsStringData();
        public int PendingBytes;
        public float CongestionLevel;
        public long TotalBytesSent;
        public long TotalBytesReceived;
        public long DeltaBytesReceived;
        public long DeltaBytesSent;
        public long TotalMessageSent;
        public long TotalMessageReceived;
        public long DeltaMessageSent;
        public long DeltaMessageReceived;
        private long lastTimeStamp;
        private long currentTimestamp;

        public float SendRate;
        public float ReceiveRate;
        public float MessageDispatchRate;
        public float MessageReceiveRate;

        public TcpStatistics()
        {
        }

        public TcpStatistics(SessionStatistics refstats)
        {
            PendingBytes = refstats.PendingBytes;
            CongestionLevel = refstats.CongestionLevel;
            TotalBytesSent = refstats.TotalBytesSent;
            TotalBytesReceived = refstats.TotalBytesReceived;
            TotalMessageSent = refstats.TotalMessageDispatched;
            TotalMessageReceived = refstats.TotalMessageReceived;

            DeltaBytesReceived = refstats.TotalBytesSent;
            DeltaBytesSent = refstats.TotalBytesReceived;
            DeltaMessageReceived = refstats.TotalMessageReceived;
            DeltaMessageSent = refstats.TotalMessageDispatched;

            lastTimeStamp = 0;
            currentTimestamp = 0;
            SendRate = 0;
            ReceiveRate = 0;
        }
        internal void Update(SessionStatistics refstats, long elapsedMilliseconds)
        {
            lastTimeStamp = currentTimestamp;
            currentTimestamp = elapsedMilliseconds;
            var deltaTMs = currentTimestamp - lastTimeStamp;

            DeltaBytesReceived = refstats.DeltaBytesReceived;
            DeltaBytesSent = refstats.DeltaBytesSent;

            SendRate = (float)((refstats.DeltaBytesSent) / (double)(1 + deltaTMs)) * 1000;
            ReceiveRate = (float)((refstats.DeltaBytesReceived) / (double)(1 + deltaTMs)) * 1000;

            MessageDispatchRate = (float)((refstats.TotalMessageDispatched - TotalMessageSent) / (double)(1 + deltaTMs)) * 1000;
            MessageReceiveRate = (float)((refstats.TotalMessageReceived - TotalMessageReceived) / (double)(1 + deltaTMs)) * 1000;

            PendingBytes = refstats.PendingBytes;
            CongestionLevel = refstats.CongestionLevel;
            TotalBytesSent = refstats.TotalBytesSent;
            TotalBytesReceived = refstats.TotalBytesReceived;
            TotalMessageSent = refstats.TotalMessageDispatched;
            TotalMessageReceived = refstats.TotalMessageReceived;

            DeltaMessageReceived = refstats.DeltaMessageReceived;
            DeltaMessageSent = refstats.DeltaMessageSent;
        }

        internal void Clear()
        {
            PendingBytes = 0;
            CongestionLevel = 0;

            MessageDispatchRate = 0;
            MessageReceiveRate = 0;

            lastTimeStamp = 0;
            currentTimestamp = 0;
            SendRate = 0;
            ReceiveRate = 0;

            //TotalMessageReceived= 0;
            //TotalMessageSent= 0;
        }

        public static TcpStatistics GetAverageStatistics(List<TcpStatistics> statList)
        {
            var averageStats = new TcpStatistics();
            foreach (var stat in statList)
            {
                averageStats.PendingBytes += stat.PendingBytes;
                averageStats.CongestionLevel += stat.CongestionLevel;
                averageStats.TotalBytesSent += stat.DeltaBytesSent;
                averageStats.TotalBytesReceived += stat.DeltaBytesReceived;
                averageStats.SendRate += stat.SendRate;
                averageStats.ReceiveRate += stat.ReceiveRate;
                averageStats.TotalMessageSent += stat.TotalMessageSent;
                averageStats.TotalMessageReceived += stat.TotalMessageReceived;
                averageStats.MessageDispatchRate += stat.MessageDispatchRate;
                averageStats.MessageReceiveRate += stat.MessageReceiveRate;
            }
            averageStats.CongestionLevel /= statList.Count;
            return averageStats;
        }



        public override string ToString()
        {

            return string.Format("# TCP :\n" +
                "Total Pending Bytes:         {0}\n" +
                "Total Congestion Level:      {1}\n" +
                "Total Bytes Sent:            {2}\n" +
                "Total Bytes Received:        {3}\n" +
                "Total Message Sent:          {6}\n" +
                "Total Message Received:      {7}\n" +
                "Data Send Rate:              {4}\n" +
                "Data Receive Rate:           {5}\n" +

                "Message Send Rate:           {8} Msg/s\n" +
                "Message Receive Rate:        {9} Msg/s\n",

                 UdpStatisticsPublisher.BytesToString(PendingBytes),
                 CongestionLevel.ToString("P"),
                 TcpServerStatisticsPublisher.BytesToString(TotalBytesSent),
                 TcpServerStatisticsPublisher.BytesToString(TotalBytesReceived),
                 TcpServerStatisticsPublisher.BytesToString((long)SendRate) + "/s",
                 TcpServerStatisticsPublisher.BytesToString((long)ReceiveRate) + "/s",
                 TotalMessageSent.ToString("N0"),
                 TotalMessageReceived.ToString("N0"),
                 MessageDispatchRate.ToString("N1"),
                 MessageReceiveRate.ToString("N1"));
        }

        public TcpStatisticsStringData Stringify()
        {
            sessionStatsJson.PendingBytes = UdpStatisticsPublisher.BytesToString(PendingBytes);
            sessionStatsJson.CongestionLevel = CongestionLevel.ToString("P");
            sessionStatsJson.TotalBytesSent = TcpServerStatisticsPublisher.BytesToString(TotalBytesSent);
            sessionStatsJson.TotalBytesReceived = TcpServerStatisticsPublisher.BytesToString(TotalBytesReceived);
            sessionStatsJson.SendRate = TcpServerStatisticsPublisher.BytesToString((long)SendRate) + "/s";
            sessionStatsJson.ReceiveRate = TcpServerStatisticsPublisher.BytesToString((long)ReceiveRate) + "/s";
            sessionStatsJson.MessageDispatchRate = MessageDispatchRate.ToString("N1");
            sessionStatsJson.TotalMessageDispatched = TotalMessageSent.ToString();
            sessionStatsJson.TotalMessageReceived = TotalMessageReceived.ToString();

            return sessionStatsJson;
        }
    }

    public readonly ref struct SessionStatistics
    {
        public readonly int PendingBytes;
        public readonly float CongestionLevel;
        public readonly long TotalBytesSent;
        public readonly long TotalBytesReceived;
        public readonly long DeltaBytesReceived;
        public readonly long DeltaBytesSent;
        public readonly long TotalMessageReceived;
        public readonly long TotalMessageDispatched;
        public readonly long DeltaMessageSent;
        public readonly long DeltaMessageReceived;

        public SessionStatistics(int pendingBytes,
                                 float congestionLevel,
                                 long totalBytesSent,
                                 long totalBytesReceived,
                                 long deltaBytesSent,
                                 long deltaBytesReveived,
                                 long totalMessageSent,
                                 long totalMessageReceived,
                                 long deltaMessageSent,
                                 long deltaMessageReceived)
        {
            PendingBytes = pendingBytes;
            CongestionLevel = congestionLevel;
            TotalBytesSent = totalBytesSent;
            TotalBytesReceived = totalBytesReceived;
            TotalMessageDispatched = totalMessageSent;
            DeltaBytesReceived = deltaBytesReveived;
            DeltaBytesSent = deltaBytesSent;
            TotalMessageReceived = totalMessageReceived;
            DeltaMessageSent = deltaMessageSent;
            DeltaMessageReceived = deltaMessageReceived;
        }


    }
    internal class TcpServerStatisticsPublisher
    {
        internal ConcurrentDictionary<Guid, TcpStatistics> Stats { get; } = new ConcurrentDictionary<Guid, TcpStatistics>();
        internal readonly ConcurrentDictionary<Guid, IAsyncSession> Sessions = new ConcurrentDictionary<Guid, IAsyncSession>();
        private TcpStatistics generalStats = new TcpStatistics();
        private Stopwatch sw = new Stopwatch();
        static string[] dataSuffix = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
        public TcpServerStatisticsPublisher(in ConcurrentDictionary<Guid, IAsyncSession> sessions)
        {
            Sessions = sessions;
            sw.Start();

        }

        private void GetSessionStats()
        {
            int count = 1;
            generalStats.Clear();
            foreach (var item in Stats.Keys)
            {
                if (!Sessions.ContainsKey(item))
                    Stats.TryRemove(item, out _);
            }
            foreach (var session in Sessions)
            {
                if (Stats.ContainsKey(session.Key))
                {
                    var val = session.Value.GetSessionStatistics();
                    Stats[session.Key].Update(val, sw.ElapsedMilliseconds);//here
                }
                else
                {
                    var val = session.Value.GetSessionStatistics();
                    Stats[session.Key] = new TcpStatistics(val);
                }

                generalStats.PendingBytes += Stats[session.Key].PendingBytes;
                generalStats.CongestionLevel += Stats[session.Key].CongestionLevel;
                generalStats.TotalBytesSent += Stats[session.Key].DeltaBytesSent;
                generalStats.TotalBytesReceived += Stats[session.Key].DeltaBytesReceived;
                generalStats.SendRate += Stats[session.Key].SendRate;
                generalStats.ReceiveRate += Stats[session.Key].ReceiveRate;
                generalStats.TotalMessageSent += Stats[session.Key].DeltaMessageSent;
                generalStats.TotalMessageReceived += Stats[session.Key].DeltaMessageReceived;
                generalStats.MessageDispatchRate += Stats[session.Key].MessageDispatchRate;
                generalStats.MessageReceiveRate += Stats[session.Key].MessageReceiveRate;
                count++;
            }
            generalStats.CongestionLevel /= count;

        }


        public static string BytesToString(long byteCount)
        {
            if (byteCount == 0)
                return "0" + dataSuffix[0];

            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString() + " " + dataSuffix[place];
        }

        internal void GetStatistics(out TcpStatistics generalStats, out ConcurrentDictionary<Guid, TcpStatistics> sessionStats)
        {
            GetSessionStats();
            generalStats = this.generalStats;
            sessionStats = Stats;
        }
    }


}
