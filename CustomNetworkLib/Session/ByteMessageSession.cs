using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CustomNetworkLib
{
    public class ByteMessageSession : TcpSession
    {
        ByteMessageManager messageManager;
        
        public ByteMessageSession(SocketAsyncEventArgs acceptedArg, Guid sessionId) : base(acceptedArg, sessionId)
        {
            messageManager = new ByteMessageManager(sessionId,base.sendBufferSize);
            messageManager.OnMessageReady += HandleMessage;
            prefixLenght = 4;

        }

        private void HandleMessage(byte[] buffer, int offset, int count)
        {
             base.HandleRecieveComplete(buffer, offset, count);
        }

        protected override void HandleRecieveComplete(byte[] buffer, int offset, int count)
        {
            messageManager.ParseBytes(buffer, offset, count);
        }

        protected override void WriteMessagePrefix(ref byte[] buffer, int offset, int messageLength)
        {
            BufferManager.WriteInt32AsBytes(ref buffer, offset, messageLength);
        }

       
        

    }
}

