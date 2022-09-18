﻿using NetworkLibrary.TCP.Base;
using NetworkLibrary.TCP.Base.Interface;
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
        public ByteMessageTcpServer(int port, int maxClients = 100) : base(port, maxClients)
        { }

        protected override IAsyncSession CreateSession(SocketAsyncEventArgs e, Guid sessionId, BufferProvider bufferManager)
        {
            var session = new ByteMessageSession(e, sessionId, bufferManager);
            session.socketSendBufferSize = ClientSendBufsize;
            session.socketRecieveBufferSize = ClientReceiveBufsize;
            session.maxIndexedMemory = MaxIndexedMemoryPerClient;
            session.dropOnCongestion = DropOnBackPressure;
            return session;

        }
    }
}