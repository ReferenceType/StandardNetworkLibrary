using NetworkLibrary.Components;
using NetworkLibrary.Components.MessageQueue;
using NetworkLibrary.TCP.SSL.Base;
using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Text;

namespace NetworkLibrary.TCP.SSL.ByteMessage
{
    internal class SSLByteMessageSession : SslSession
    {
        ByteMessageReader reader;

        public SSLByteMessageSession(Guid sessionId, SslStream sessionStream) : base(sessionId, sessionStream)
        {
            reader = new ByteMessageReader(sessionId);
            reader.OnMessageReady += HandleMessage;
        }

        private void HandleMessage(byte[] arg1, int arg2, int arg3)
        {
            base.HandleReceived(arg1, arg2, arg3);
        }
        protected override void HandleReceived(byte[] buffer, int offset, int count)
        {
            reader.ParseBytes(buffer, offset, count);
        }

        protected override void CreateMessageQueue()
        {
            messageQueue = new FramedMessageQueue(MaxIndexedMemory);
        }

    }
}
