using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NetworkLibrary.DistributedP2P.Channels.Components
{
/*
 * Send keep alive every 5 seconds
 * 
 * if reply is not received within 1 sec resend up to 5 times
 * 
 * otherwise keep alive again 5 seconds later
 * 
 * 
 */
    internal class KeepAlive
    {
        public Action<MessageFlags, byte[], int, int> SendData;

        DateTime lastReceived = DateTime.Now;
        bool stop = false;
        int maxRetry = 10;
        byte[] innerBuff = new byte[1];
        public KeepAlive()
        {
            StartSendRoutine();
        }

        private async void StartSendRoutine()
        {
            while (!stop)
            {
                await Task.Delay(5000);
                SendKeepAlive();
                
                await Task.Delay(1000);
                int retires = 0;
                while((DateTime.Now - lastReceived).TotalMilliseconds > 1000)
                {
                    if(retires > maxRetry)
                    {
                        DisconnectDetected();
                        return;
                    }
                        
                    SendKeepAlive();
                    await Task.Delay(1000);
                }
            }
        }

        private void DisconnectDetected()
        {
           // event of DC
        }

        public void HandleMessage(MessageFlags flag, byte[] buffer, int offset, int count)
        {

            switch (flag)
            {
                case MessageFlags.KeepAliveMessage:
                    HandleKeepAlive(buffer, offset, count);
                    break;
            }
        }

        private void SendKeepAlive()
        {
            SendData?.Invoke(MessageFlags.KeepAliveMessage, innerBuff, 0, 1);
        }

        private void HandleKeepAlive(byte[] buffer, int offset, int count)
        {
            lastReceived = DateTime.Now;
        }
    }
}
