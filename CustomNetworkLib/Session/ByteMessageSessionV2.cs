using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CustomNetworkLib
{
    internal class ByteMessageSessionV2 : TcpSession
    {
        ByteMessageManager messageManager;
        public ByteMessageSessionV2(SocketAsyncEventArgs acceptedArg, Guid sessionId) : base(acceptedArg, sessionId)
        {
            messageManager = new ByteMessageManager(sessionId);
            messageManager.OnMessageReady += HandleMessage;
        }

        private void HandleMessage(byte[] buffer, int offset, int count)
        {
             base.HandleRecieveComplete(buffer, offset, count);
        }

        protected override void HandleRecieveComplete(byte[] buffer, int offset, int count)
        {
            messageManager.ParseBytes(buffer, offset, count);
        }

        protected override void Send(byte[] bytes)
        {
            if (bytes.Length + 4 > sendBuffer.Length)
            {
                sendBuffer = new byte[bytes.Length + 4];
                //ClientSendEventArg.SetBuffer(sendBuffer, 0, sendBuffer.Length);
                //GC.Collect();

            }

            byte[] byteFrame = BitConverter.GetBytes(bytes.Length);
            for (int i = 0; i < 4; i++)
            {
                sendBuffer[i] = byteFrame[i];
            }

   
            Buffer.BlockCopy(bytes, 0, sendBuffer, 4, bytes.Length);

            ClientSendEventArg.SetBuffer(sendBuffer,0,bytes.Length+4);
            if (!sessionSocket.SendAsync(ClientSendEventArg))
            {
                Sent(null, ClientSendEventArg);
            }
        }

    }
}

