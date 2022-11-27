using NetworkLibrary.TCP.Base;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetworkLibrary.TCP.ByteMessage
{
    public class ByteMessageTcpClient : AsyncTpcClient
    {

        public ByteMessageTcpClient()
        { }

        internal override IAsyncSession CreateSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            var session = new ByteMessageSession(e, sessionId);
            session.socketSendBufferSize = SocketSendBufferSize;
            session.socketRecieveBufferSize = SocketRecieveBufferSize;
            session.maxIndexedMemory = MaxIndexedMemory;
            session.dropOnCongestion = DropOnCongestion;
            session.OnSessionClosed += (id) => OnDisconnected?.Invoke();

            if (GatherConfig == ScatterGatherConfig.UseQueue)
                session.UseQueue = true;
            else
                session.UseQueue = false;

            return session;

        }

    }
}
