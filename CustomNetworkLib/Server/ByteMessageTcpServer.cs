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
        public bool Lite = true;
        public ByteMessageTcpServer(int port): base(port)
        {}

        protected override IAsyncSession CreateSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            if(Lite)
                return new ByteMessageSession(e,sessionId);
            else
                return new ByteMessageSessionLite(e,sessionId);
        }

       
       
    }
}
