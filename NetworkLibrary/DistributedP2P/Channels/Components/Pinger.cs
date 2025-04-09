using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace NetworkLibrary.DistributedP2P.Channels.Components
{

    public class Pinger
    {

        public Action<MessageFlags, byte[], int, int> SendData;
        byte[] innerBuffer = new byte[16];
        ConcurrentDictionary<Guid, TaskCompletionSource<DateTime>> dispatched = new ConcurrentDictionary<Guid, TaskCompletionSource<DateTime>>();
        public void HandleMessage(MessageFlags flag, byte[] buffer, int offset, int count)
        {
            switch (flag)
            {
                case MessageFlags.Ping:
                    HandlePing(buffer, offset, count);
                    break;
                case MessageFlags.Pong:
                    HandlePong(buffer, offset);
                    break;
            }
        }

        //[A]
        public async Task<double> Ping()
        {
            Guid guid = Guid.NewGuid();
            var pong = new TaskCompletionSource<DateTime>();
            int offset = 0;
            PrimitiveEncoder.WriteGuid(innerBuffer, ref offset, guid);
            dispatched[guid] =  pong;

            var sendTime = DateTime.Now;
            SendData?.Invoke(MessageFlags.Ping, innerBuffer, 0, 16);

            var result = await Task.WhenAny(pong.Task, Task.Delay(10000));
            if (result == pong.Task)
            {
                var replyTime = await pong.Task;
                return (replyTime - sendTime).TotalMilliseconds;
            }
            else
            {
                dispatched.TryRemove(guid, out _);
                return -1;
            }
        }

        //[B]
        private void HandlePing(byte[] buffer, int offset, int count)
        {
            SendData?.Invoke(MessageFlags.Pong, buffer, offset, count);
        }

        //[A]
        private void HandlePong(byte[] buffer, int offset)
        {
            var guid = PrimitiveEncoder.ReadGuid(buffer, ref offset);
            if (dispatched.TryRemove(guid, out var tcs))
            {
                tcs.TrySetResult(DateTime.Now);
            }
        }

    }
}
