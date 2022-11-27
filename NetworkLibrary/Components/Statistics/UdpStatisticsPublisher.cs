using NetworkLibrary.UDP;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Schema;

namespace NetworkLibrary.Components.Statistics
{
    public class UdpStatistics
    {
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

        private const string seed = "\nTotal Datagram Sent: {0} Total Datagram Received: {1} Total Datagram Dropped {2}" +
                    "\nTotal Bytes Send: {3} Total Bytes Received: {4}" +
                    "\nSend PPS: {5} Recceive PPS: {6} Send Data Rate: {7} MB/s Receive Data Rate: {8} MB/s";



        public override string ToString()
        {
            return string.Format(seed,
                          TotalDatagramSent.ToString("N1"),
                          TotalDatagramReceived.ToString("N1"),
                          TotalMessageDropped,
                          UdpStatisticsPublisher.BytesToString(TotalBytesSent),
                          UdpStatisticsPublisher.BytesToString(TotalBytesReceived),
                          SendPPS.ToString("N1"),
                          ReceivePPS.ToString("N1"),
                          SendRate.ToString("N3"),
                          ReceiveRate.ToString("N3"));
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
            foreach (var stat in Statistics)
            {
                long tsCurrent = sw.ElapsedMilliseconds;
                double deltaT = tsCurrent - stat.Value.PrevTimeStamp;

                stat.Value.SendPPS = (float)((stat.Value.TotalDatagramSent - stat.Value.TotalDatagramSentPrev) / deltaT) * 1000f;
                stat.Value.ReceivePPS = (float)((stat.Value.TotalDatagramReceived - stat.Value.TotalDatagramReceivedPrev) / deltaT) * 1000f;
                stat.Value.SendRate = (float)((stat.Value.TotalBytesSent - stat.Value.TotalBytesSentPrev) / deltaT) / 1024;
                stat.Value.ReceiveRate = (float)((stat.Value.TotalBytesReceived - stat.Value.TotalBytesReceivedPrev) / deltaT) / 1024;

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
            return (Math.Sign(byteCount) * num).ToString() + dataSuffix[place];
        }

        internal void GetStatistics(out UdpStatistics generalStats, out ConcurrentDictionary<IPEndPoint, UdpStatistics> sessionStats)
        {
            GatherStatistics();
            generalStats = generalstats;
            sessionStats = Statistics;
        }
    }
}
