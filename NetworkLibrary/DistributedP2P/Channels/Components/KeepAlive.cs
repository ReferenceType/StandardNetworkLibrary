using System;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace NetworkLibrary.DistributedP2P.Channels.Components
{

    public class KeepAlive
    {
        public Action<MessageFlags, byte[], int, int> SendData;
        public Action NotAlive;

        private DateTime lastReceived = DateTime.Now;
        private bool stop = false;
        private byte[] innerBuff = new byte[32];
        public KeepAlive()
        {
            StartSendRoutine();
        }

        private async void StartSendRoutine()
        {
            while (!stop)
            {
                await Task.Delay(4000);
                if (stop) break; 
                SendKeepAlive();

                if ((DateTime.Now - lastReceived).TotalMilliseconds > 10000)
                {
                    DisconnectDetected();
                }
            }
        }

        private void DisconnectDetected()
        {
            NotAlive?.Invoke();
        }

        public void HandleMessage(MessageFlags flag, byte[] buffer, int offset, int count)
        {
            HandleKeepAlive(buffer, offset, count);            
        }

        RandomNumberGenerator r = RandomNumberGenerator.Create();
        private void SendKeepAlive()
        {
            r.GetBytes(innerBuff, 0, 32);
            SendData?.Invoke(MessageFlags.KeepAliveMessage, innerBuff, 0, 32);
            Console.WriteLine("Keep alive sent");
        }

        private void HandleKeepAlive(byte[] buffer, int offset, int count)
        {
            lastReceived = DateTime.Now;
            Console.WriteLine("Keep alive received");
        }

        internal void Close()
        {
            stop = true;
            NotAlive = null;
            SendData = null;
        }
    }
}
