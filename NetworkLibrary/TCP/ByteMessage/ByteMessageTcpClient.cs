using NetworkLibrary.TCP.Base;
using System;
using System.Net.Sockets;

namespace NetworkLibrary.TCP.ByteMessage
{
    /// <summary>
    /// Sends and reveives messages with 4 byte lenght header.
    /// messages are guarantied to be received atomically without fragmentation.
    /// </summary>
    public class ByteMessageTcpClient : AsyncTpcClient
    {

        public ByteMessageTcpClient()
        { }

        private protected override IAsyncSession CreateSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            var session = new ByteMessageSession(e, sessionId);
            session.socketSendBufferSize = SocketSendBufferSize;
            session.SocketRecieveBufferSize = SocketRecieveBufferSize;
            session.MaxIndexedMemory = MaxIndexedMemory;
            session.DropOnCongestion = DropOnCongestion;

            if (GatherConfig == ScatterGatherConfig.UseQueue)
                session.UseQueue = true;
            else
                session.UseQueue = false;

            return session;

        }

    }
}
