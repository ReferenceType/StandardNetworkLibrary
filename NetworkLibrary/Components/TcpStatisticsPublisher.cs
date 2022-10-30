using NetworkLibrary.TCP.Base;
using System;
using System.CodeDom.Compiler;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using static NetworkLibrary.TCP.Base.AsyncTcpServer;

namespace NetworkLibrary.Components
{
    public class SessionStats
    {
        public int PendingBytes;
        public float CongestionLevel;
        public long TotalBytesSent;
        public long TotalBytesReceived;
        public long TotalMessageDispatched;
        private long lastTimeStamp;
        private long currentTimestamp;

        public float SendRate;
        public float ReceiveRate;
        public float MessageDispatchRate;

        public SessionStats()
        {

        }
        
        public SessionStats(SessionStatistics refstats)
        {
            PendingBytes = refstats.PendingBytes;
            CongestionLevel = refstats.CongestionLevel;
            TotalBytesSent = refstats.TotalBytesSent;
            TotalBytesReceived = refstats.TotalBytesReceived;
            TotalMessageDispatched = refstats.TotalMessageDispatched;

            lastTimeStamp = 0;
            currentTimestamp = 0;
            SendRate = 0;
            ReceiveRate = 0;
        }
        public void Update(SessionStatistics refstats, long elapsedMilliseconds)
        {
            lastTimeStamp = currentTimestamp;
            currentTimestamp = elapsedMilliseconds;

            SendRate = (float)((refstats.TotalBytesSent - TotalBytesSent) / (double)(1+(currentTimestamp - lastTimeStamp)))/1000;
            ReceiveRate = (float)((refstats.TotalBytesReceived - TotalBytesReceived) / (double)(1+(currentTimestamp - lastTimeStamp)))/1000;
            MessageDispatchRate = (float)((refstats.TotalMessageDispatched - TotalMessageDispatched) / (double)(1 + (currentTimestamp - lastTimeStamp)))*1000;

            PendingBytes = refstats.PendingBytes;
            CongestionLevel = refstats.CongestionLevel;
            TotalBytesSent = refstats.TotalBytesSent;
            TotalBytesReceived = refstats.TotalBytesReceived;
            TotalMessageDispatched = refstats.TotalMessageDispatched;
        }

        internal void Clear()
        {
            PendingBytes = 0;
            CongestionLevel = 0;
            TotalBytesSent = 0;
            TotalBytesReceived = 0;
            TotalMessageDispatched = 0;
            MessageDispatchRate = 0;

            lastTimeStamp = 0;
            currentTimestamp = 0;
            SendRate = 0;
            ReceiveRate = 0;
        }

        public override string ToString()
        {
            return String.Format("\nTCP : Total Pending: {0} Total Congestion: {1}  Total Sent: {2}   Total Received: {3}  " +
                "\nTotal SendRate: {4} MBs/s, Total ReceiveRate: {5}MBs/s  Total Message Send Rate: {6}"
                   ,PendingBytes,
                   CongestionLevel.ToString("P"),
                   TcpStatisticsPublisher.BytesToString(TotalBytesSent),
                   TcpStatisticsPublisher.BytesToString(TotalBytesReceived),
                   SendRate.ToString("N1"),
                   ReceiveRate.ToString("N1"),
                   MessageDispatchRate.ToString("N1"));
        }
    }

    public readonly ref struct SessionStatistics
    {
        public readonly int PendingBytes;
        public readonly float CongestionLevel;
        public readonly long TotalBytesSent;
        public readonly long TotalBytesReceived;
        public readonly long TotalMessageDispatched;

        public SessionStatistics(int pendingBytes, float congestionLevel, long totalBytesSent, long totalBytesReceived, long totalMessageSent)
        {
            PendingBytes = pendingBytes;
            CongestionLevel = congestionLevel;
            TotalBytesSent = totalBytesSent;
            TotalBytesReceived = totalBytesReceived;
            TotalMessageDispatched = totalMessageSent;
        }

        
    }
    internal class TcpStatisticsPublisher
    {
        internal ConcurrentDictionary<Guid, SessionStats> Stats { get; } = new ConcurrentDictionary<Guid, SessionStats>(); 
        internal readonly ConcurrentDictionary<Guid, IAsyncSession> Sessions = new ConcurrentDictionary<Guid, IAsyncSession>();
        private SessionStats generalStats = new SessionStats();
        private Stopwatch sw = new Stopwatch();
        static string[] dataSuffix = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };

        public TcpStatisticsPublisher(in ConcurrentDictionary<Guid, IAsyncSession> sessions,int publushFrequencyMs)
        {
            Sessions = sessions;
            sw.Start();
            //Task.Run(async () =>
            //{
            //    while (true)
            //    {
            //        try
            //        {
            //            await Task.Delay(publushFrequencyMs);
            //            GetSessionStats();
            //            PublishSessionStats();
            //        }
            //        catch
            //        {

            //        }
                   
            //    }
            //});
        }

        private void GetSessionStats()
        {
            int count = 0;
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
                    Stats[session.Key].Update(val,sw.ElapsedMilliseconds);
                }
                else
                {
                    var val = session.Value.GetSessionStatistics();
                    Stats[session.Key] = new SessionStats(val);
                }

                generalStats.PendingBytes += Stats[session.Key].PendingBytes;
                generalStats.CongestionLevel += Stats[session.Key].CongestionLevel;
                generalStats.TotalBytesSent += Stats[session.Key].TotalBytesSent;
                generalStats.TotalBytesReceived += Stats[session.Key].TotalBytesReceived;
                generalStats.SendRate += Stats[session.Key].SendRate;
                generalStats.ReceiveRate += Stats[session.Key].ReceiveRate;
                generalStats.MessageDispatchRate += Stats[session.Key].MessageDispatchRate;
                count++;
            }
            generalStats.CongestionLevel /= count;

        }
        private void PublishSessionStats()
        {
      
            int count = 0;

            foreach (var item in Stats)
            {
                //generalStats.PendingBytes += item.Value.PendingBytes;
                //generalStats.CongestionLevel += item.Value.CongestionLevel;
                //generalStats.TotalBytesSent+= item.Value.TotalBytesSent;
                //generalStats.TotalBytesReceived+=item.Value.TotalBytesReceived;
                //generalStats.SendRate+=item.Value.SendRate;
                //generalStats.ReceiveRate += item.Value.ReceiveRate;
                //generalStats.MessageDispatchRate += item.Value.MessageDispatchRate;
                //count++;

            }
            //generalStats.CongestionLevel /= count;

            //Console.WriteLine(String.Format("\nServser Sum : Total Pending: {0} Total Congestion: {1}  Total Sent: {2}   Total Received: {3}  " +
            //    "\nTotal SendRate: {4} MBs/s, Total ReceiveRate: {5}MBs/s  Total Message Send Rate: {6}"
            //       , generalStats.PendingBytes,
            //       generalStats.CongestionLevel.ToString("P"),
            //       BytesToString(generalStats.TotalBytesSent),
            //       BytesToString(generalStats.TotalBytesReceived),
            //       generalStats.SendRate.ToString("N1"),
            //       generalStats.ReceiveRate.ToString("N1"),
            //       generalStats.TotalMessageDispatched.ToString("N1")
            //       )); ;
           
        }
        
       public static String BytesToString(long byteCount)
        {
            if (byteCount == 0)
                return "0" + dataSuffix[0];

            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString() + dataSuffix[place];
        }

        internal void GetStatistics(out SessionStats generalStats, out ConcurrentDictionary<Guid, SessionStats> sessionStats)
        {
            GetSessionStats();
            generalStats = this.generalStats;
            sessionStats = this.Stats;
        }
    }


}
