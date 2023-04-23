using NetworkLibrary.TCP.Base;
using System;
using System.Net.Sockets;

namespace NetworkLibrary.TCP.ByteMessage
{
    public class ByteMessageTcpClient : AsyncTpcClient
    {

        public ByteMessageTcpClient()
        { }

        protected override IAsyncSession CreateSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            var session = new ByteMessageSession(e, sessionId);
            session.socketSendBufferSize = SocketSendBufferSize;
            session.SocketRecieveBufferSize = SocketRecieveBufferSize;
            session.MaxIndexedMemory = MaxIndexedMemory;
            session.DropOnCongestion = DropOnCongestion;
            session.OnSessionClosed += (id) => OnDisconnected?.Invoke();

            if (GatherConfig == ScatterGatherConfig.UseQueue)
                session.UseQueue = true;
            else
                session.UseQueue = false;

            return session;

        }

    }
}
