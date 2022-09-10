using CustomNetworkLib.Utils;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CustomNetworkLib
{
    internal class ByteMessageSession : TcpSession
    {
        ByteMessageReader messageManager;
        public ByteMessageSession(SocketAsyncEventArgs acceptedArg, Guid sessionId, BufferProvider bufferManager) : base(acceptedArg, sessionId, bufferManager)
        {
            prefixLenght = 4;
        }

        public override void StartSession()
        {
            messageManager = new ByteMessageReader(SessionId, socketRecieveBufferSize);
            messageManager.OnMessageReady += HandleMessage;

            base.StartSession();
        }

        private void HandleMessage(byte[] buffer, int offset, int count)
        {
             base.HandleRecieveComplete(buffer, offset, count);
        }

        // We take the received from the base here put it on msg reader,
        // for each extracted message reader will call handle message,
        // which will call base HandleRecieveComplete to triger message received event.
        protected override void HandleRecieveComplete(byte[] buffer, int offset, int count)
        {
            messageManager.ParseBytes(buffer, offset, count);
        }

        protected override void WriteMessagePrefix(ref byte[] buffer, int offset, int messageLength)
        {
            PrefixWriter.WriteInt32AsBytes(ref buffer, offset, messageLength);
        }

    }
}

