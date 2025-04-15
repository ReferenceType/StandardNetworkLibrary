using NetworkLibrary.UDP;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;

namespace NetworkLibrary.DistributedP2P.Components
{
    public class TimeResult
    {
        public double PreciseTime { get; set; }
        public TimeSpan DateTimeOffset { get; set; }
        public bool Succes { get; set; }
        public double RTT { get; set; }
        public double Delay { get; set; }  // Store calculated delay
        public double AsymmetryEstimate { get; set; } // Store estimated path asymmetry
    }

   
    internal class NTPClient:IDisposable
    {

        struct TimeData
        {
            public double ServersTime;
            public double ArrivalTime;
        }


        ConcurrentDictionary<byte, TaskCompletionSource<TimeData>> pending = new ConcurrentDictionary<byte, TaskCompletionSource<TimeData>>();
        byte ctr = 0;
        private AsyncUdpClient udpClient;
        private Stopwatch clientClock;

        public NTPClient(string ip, int port, Stopwatch clientClock)
        {
            udpClient = new AsyncUdpClient();
            udpClient.OnBytesRecieved += HandleReceived;
            udpClient.Connect(ip, port);

            this.clientClock = clientClock;
        }

        private void HandleReceived(byte[] bytes, int offset, int count)
        {
            var arrivalTime = clientClock.Elapsed.TotalMilliseconds;
            byte num = bytes[offset++];
            if (pending.TryRemove(num, out var tcs))
            {
                double time = PrimitiveEncoder.ReadFixedDouble(bytes, offset);
                tcs.SetResult(new TimeData() { ServersTime = time, ArrivalTime = arrivalTime });
            }
        }

        public async Task<TimeResult> GetServerTime(int delayMs)
        {
            var buff = new byte[1];
            var pollNum = ctr++;
            buff[0] = pollNum;
            var tcs = new TaskCompletionSource<TimeData>();
            pending[pollNum] = tcs;

            var t1 = clientClock.Elapsed.TotalMilliseconds;
            udpClient.SendAsync(buff);

            var delayTask = Task.Delay(delayMs);
            if (await Task.WhenAny(delayTask, tcs.Task).ConfigureAwait(false)== tcs.Task)
            {

                var serverTime = tcs.Task.Result;
                var t2 = serverTime.ServersTime;
                var t3 = t2 + 0.005;
                var t4 = serverTime.ArrivalTime;


                var delay = (t4 - t1) - (t3 - t2);
                var offset = ((t2 - t1) + (t3 - t4)) / 2;

                return new TimeResult()
                {
                    PreciseTime = offset,
                    Succes = true,
                    RTT = t4 - t1,
                    Delay = delay,
                };
            }
            else
            {
                pending.TryRemove(pollNum, out _);
                return new TimeResult()
                {
                    Succes = false
                };
            }




        }

        public void Dispose()
        {
            udpClient?.Dispose();
        }
    }
}
