using NetworkLibrary.Components;
using NetworkLibrary.Components.MessageBuffer;
using NetworkLibrary.Components.MessageProcessor.Unmanaged;
using NetworkLibrary.TCP.Base;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetworkLibrary.TCP.ByteMessage
{
    internal class ByteMessageSession : TcpSession
    {
        ByteMessageReader messageManager= null;
        public ByteMessageSession(SocketAsyncEventArgs acceptedArg, Guid sessionId) : base(acceptedArg, sessionId)
        {
        }

        public override void StartSession()
        {
            messageManager = new ByteMessageReader(SessionId, socketRecieveBufferSize);
            messageManager.OnMessageReady += HandleMessage;

            base.StartSession();
        }

        protected virtual void HandleMessage(byte[] buffer, int offset, int count)
        {
            base.HandleReceived(buffer, offset, count);
        }

        // We take the received from the base here put it on msg reader,
        // for each extracted message reader will call handle message,
        // which will call base HandleRecieveComplete to triger message received event.
        protected sealed override void HandleReceived(byte[] buffer, int offset, int count)
        {
            messageManager.ParseBytes(buffer, offset, count);
        }

        protected override IMessageQueue CreateMessageQueue()
        {
            if (UseQueue)
            {
                var q = new MessageQueue<UnsafeDelimitedMessageWriter>(MaxIndexedMemory, new UnsafeDelimitedMessageWriter());
                return q;
            }
            else
            {
                return new MessageBuffer(MaxIndexedMemory);
            }

        }


    }
}

