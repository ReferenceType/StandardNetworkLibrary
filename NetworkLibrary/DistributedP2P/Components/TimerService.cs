using NetworkLibrary.DistributedP2P.Server;
using System;
using System.Collections.Concurrent;
using System.Threading;
namespace NetworkLibrary.DistributedP2P.Components
{
    internal class TimerService
    {
        private static readonly ConcurrentDictionary<Guid, Timer> timers = new ConcurrentDictionary<Guid, Timer>();

        public static void RegisterTimer(Guid timerId,int delay, Action OnTime)
        {
            var timer = new Timer(s =>
            {
                OnTime?.Invoke();
                CancelTimeout(timerId);

            }, null, delay, Timeout.Infinite);

            timers.TryAdd(timerId, timer);
        }

        public static void CancelTimeout(Guid guid)
        {
            if (timers.TryRemove(guid, out var timer))
            {
                timer.Dispose();
            }

        }

        internal static void RegisterTimer(Guid stateId, object onTimeOut)
        {
            throw new NotImplementedException();
        }
    }
}
