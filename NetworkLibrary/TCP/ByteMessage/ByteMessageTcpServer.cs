using NetworkLibrary.TCP.Base;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace NetworkLibrary.TCP.ByteMessage
{
    public class ByteMessageTcpServer : AsyncTcpServer
    {
        public ByteMessageTcpServer(int port) : base(port)
        { }

        protected override IAsyncSession CreateSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            var session = new ByteMessageSession(e, sessionId);
            session.socketSendBufferSize = ClientSendBufsize;
            session.socketRecieveBufferSize = ClientReceiveBufsize;
            session.MaxIndexedMemory = MaxIndexedMemoryPerClient;
            session.DropOnCongestion = DropOnBackPressure;
            session.OnSessionClosed += (id) => OnClientDisconnected?.Invoke(id);


            if (GatherConfig == ScatterGatherConfig.UseQueue)
                session.UseQueue = true;
            else
                session.UseQueue = false;

            return session;

        }
    }
}
