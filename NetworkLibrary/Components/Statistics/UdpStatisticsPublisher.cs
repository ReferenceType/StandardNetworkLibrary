using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;


namespace NetworkLibrary.Components.Statistics
{
    public class UdpStatisticsStringData
    {
        public string TotalDatagramSent { get; set; }
        public string TotalDatagramReceived { get; set; }
        public string TotalBytesSent { get; set; }
        public string TotalBytesReceived { get; set; }
        public string TotalMessageDropped { get; set; }

        public string SendRate { get; set; }
        public string ReceiveRate { get; set; }
        public string SendPPS { get; set; }
        public string ReceivePPS { get; set; }


    }
    public class UdpStatistics
    {
        UdpStatisticsStringData data = new UdpStatisticsStringData();
        public long TotalDatagramSent = 0;
        public long TotalDatagramReceived = 0;
        public long TotalBytesSent = 0;
        public long TotalBytesReceived = 0;
        public long TotalMessageDropped = 0;

        public float SendRate;
        public float ReceiveRate;
        public float SendPPS;
        public float ReceivePPS;

        public long TotalDatagramSentPrev = 0;
        public long TotalDatagramReceivedPrev = 0;
        public long TotalBytesSentPrev = 0;
        public long TotalBytesReceivedPrev = 0;
        public long TotalMessageDroppedPrev = 0;

        public long PrevTimeStamp;

        private const string seed =
            "\n# Udp:\n" +
            "Total Datagram Sent:         {0}\n" +
            "Total Datagram Received:     {1}\n" +
            "Total Datagram Dropped       {2}\n" +
            "Total Bytes Send:            {3}\n" +
            "Total Bytes Received:        {4}\n" +
            "Total Datagram Send Rate:    {5} Msg/s\n" +
            "Total Datagram Receive Rate: {6} Msg/s\n" +
            "Total Send Data Rate:        {7}\n" +
            "Total Receive Data Rate:     {8} ";



        public override string ToString()
        {
            return string.Format(seed,
                          TotalDatagramSent.ToString(),
                          TotalDatagramReceived.ToString(),
                          TotalMessageDropped,
                          UdpStatisticsPublisher.BytesToString(TotalBytesSent),
                          UdpStatisticsPublisher.BytesToString(TotalBytesReceived),
                          SendPPS.ToString("N1"),
                          ReceivePPS.ToString("N1"),
                          TcpServerStatisticsPublisher.BytesToString((long)SendRate) + "/s",
                          TcpServerStatisticsPublisher.BytesToString((long)ReceiveRate) + "/s");
        }

        public UdpStatisticsStringData Stringify()
        {
            data.TotalDatagramSent = TotalDatagramSent.ToString();
            data.TotalDatagramReceived = TotalDatagramReceived.ToString();
            data.TotalMessageDropped = TotalMessageDropped.ToString();
            data.TotalBytesSent = UdpStatisticsPublisher.BytesToString(TotalBytesSent);
            data.TotalBytesReceived = UdpStatisticsPublisher.BytesToString(TotalBytesReceived);
            data.SendPPS = SendPPS.ToString("N1");
            data.ReceivePPS = ReceivePPS.ToString("N1");
            data.SendRate = TcpServerStatisticsPublisher.BytesToString((long)SendRate) + "/s";
            data.ReceiveRate = TcpServerStatisticsPublisher.BytesToString((long)ReceiveRate) + "/s";

            return data;
        }

        internal void Reset()
        {
            TotalDatagramSent = 0;
            TotalDatagramReceived = 0;
            TotalBytesSent = 0;
            TotalBytesReceived = 0;
            TotalMessageDropped = 0;
            SendRate = 0;
            ReceiveRate = 0;
            SendPPS = 0;
            ReceivePPS = 0;
            TotalDatagramSentPrev = 0;
            TotalDatagramReceivedPrev = 0;
            TotalBytesSentPrev = 0;
            TotalBytesReceivedPrev = 0;
            TotalMessageDroppedPrev = 0;
        }
    }
    internal class UdpStatisticsPublisher
    {
        private ConcurrentDictionary<IPEndPoint, UdpStatistics> Statistics;
        private UdpStatistics generalstats = new UdpStatistics();
        private Stopwatch sw = new Stopwatch();
        static string[] dataSuffix = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };


        public UdpStatisticsPublisher(ConcurrentDictionary<IPEndPoint, UdpStatistics> statistics)
        {
            sw.Start();
            Statistics = statistics;
        }



        private void GatherStatistics()
        {
            generalstats.Reset();
            // Statistics object is shared dict between udp server and this.
            foreach (var stat in Statistics)
            {
                long tsCurrent = sw.ElapsedMilliseconds;
                double deltaT = tsCurrent - stat.Value.PrevTimeStamp;

                stat.Value.SendPPS = (float)((stat.Value.TotalDatagramSent - stat.Value.TotalDatagramSentPrev) / deltaT) * 1000f;
                stat.Value.ReceivePPS = (float)((stat.Value.TotalDatagramReceived - stat.Value.TotalDatagramReceivedPrev) / deltaT) * 1000f;
                stat.Value.SendRate = (float)((stat.Value.TotalBytesSent - stat.Value.TotalBytesSentPrev) / deltaT) * 1000;
                stat.Value.ReceiveRate = (float)((stat.Value.TotalBytesReceived - stat.Value.TotalBytesReceivedPrev) / deltaT) * 1000;

                stat.Value.TotalDatagramSentPrev = stat.Value.TotalDatagramSent;
                stat.Value.TotalDatagramReceivedPrev = stat.Value.TotalDatagramReceived;
                stat.Value.TotalBytesSentPrev = stat.Value.TotalBytesSent;
                stat.Value.TotalBytesReceivedPrev = stat.Value.TotalBytesReceived;

                stat.Value.PrevTimeStamp = tsCurrent;



                var data1 = stat.Value;
                generalstats.SendPPS += data1.SendPPS;
                generalstats.SendRate += data1.SendRate;
                generalstats.ReceiveRate += data1.ReceiveRate;
                generalstats.TotalBytesReceived += data1.TotalBytesReceived;
                generalstats.TotalBytesSent += data1.TotalBytesSent;
                generalstats.TotalMessageDropped += data1.TotalMessageDropped;
                generalstats.ReceivePPS += data1.ReceivePPS;
                generalstats.TotalDatagramReceived += data1.TotalDatagramReceived;
                generalstats.TotalDatagramSent += data1.TotalDatagramSent;
            }
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

        internal void GetStatistics(out UdpStatistics generalStats, out ConcurrentDictionary<IPEndPoint, UdpStatistics> sessionStats)
        {
            GatherStatistics();
            generalStats = generalstats;
            sessionStats = Statistics;
        }
    }
}
