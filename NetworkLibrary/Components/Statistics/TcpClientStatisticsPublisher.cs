using NetworkLibrary.TCP.Base;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace NetworkLibrary.Components.Statistics
{
    internal class TcpClientStatisticsPublisher
    {
        internal ConcurrentDictionary<Guid, TcpStatistics> Stats { get; } = new ConcurrentDictionary<Guid, TcpStatistics>();
        internal readonly IAsyncSession Session;
        private TcpStatistics generalStats = new TcpStatistics();
        private Stopwatch sw = new Stopwatch();
        public TcpClientStatisticsPublisher(IAsyncSession session, in Guid sessionId)
        {
            Session = session;
            sw.Start();
        }

        private void GetSessionStats()
        {
            var stats = Session.GetSessionStatistics();
            generalStats.Update(stats, sw.ElapsedMilliseconds);

        }

        internal void GetStatistics(out TcpStatistics generalStats)
        {
            GetSessionStats();
            generalStats = this.generalStats;
        }

    }
}
