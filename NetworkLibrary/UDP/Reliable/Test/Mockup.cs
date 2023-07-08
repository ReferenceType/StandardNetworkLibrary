using NetworkLibrary.UDP.Reliable.Components;
using NetworkLibrary.Utils;
using System;
using System.Threading;

namespace NetworkLibrary.UDP.Reliable.Test
{
    public class Mockup
    {
        SenderModule s = new SenderModule();
        ReceiverModule r = new ReceiverModule();
        Random random = new Random();
        public Action<byte[], int, int> OnReceived;
        public bool RemoveNoiseSend = true;
        public bool RemoveNoiseFeedback = true;
        public int getArrivedCount() => r.arrived.Count;
        public int getActiveCount() => s.pendingPackages.Count;
        public Mockup()
        {
            s.SendRequested += EmulateNoisySend;
            r.SendFeedback += EmulateNoisyFeedback;
            r.OnMessageReceived += MessageReceived;
        }


        private void EmulateNoisyFeedback(byte[] arg1, int arg2, int arg3)
        {
            if (RemoveNoiseFeedback || random.Next(0, 100) % 30 != 0)
            {
                var buff = ByteCopy.ToArray(arg1, arg2, arg3);
                ThreadPool.UnsafeQueueUserWorkItem((x) => { s.HandleFeedback(buff, 0); }, null);
            }
            else
            {

            }
            return;

        }

        private void EmulateNoisySend(byte[] arg1, int arg2, int arg3)
        {
            if (RemoveNoiseSend || random.Next(0, 100) % 20 != 0)
            {
                var buff = ByteCopy.ToArray(arg1, arg2, arg3);

                ThreadPool.UnsafeQueueUserWorkItem((m) =>
                {
                    r.HandleBytes(buff, 0, buff.Length);
                }
                , null);
            }
            else
            {

            }
            return;

        }

        public void SendTest(byte[] arg1, int arg2, int arg3)
        {
            s.ProcessBytesToSend(arg1, arg2, arg3);
        }
        public void SendTest(Segment first, Segment second)
        {
            s.ProcessBytesToSend(first, second);
        }
        private void MessageReceived(byte[] arg1, int arg2, int arg3)
        {
            OnReceived?.Invoke(arg1, arg2, arg3);
        }
    }
}
