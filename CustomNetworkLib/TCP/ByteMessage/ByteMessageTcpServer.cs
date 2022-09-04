using CustomNetworkLib.SocketEventArgsTests;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace CustomNetworkLib
{
    public class ByteMessageTcpServer : AsyncTcpServer
    {
        public ByteMessageTcpServer(int port): base(port)
        {}

        protected override IAsyncSession CreateSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            var session = new ByteMessageSession(e, sessionId);
            session.MaxIndexedMemory = MaxIndexedMemoryPerClient;
            session.DropOnCongestion = DropOnBackPressure;
            return session;
            
        }

       
       
    }
}
