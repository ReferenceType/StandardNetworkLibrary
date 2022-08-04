using CustomNetworkLib.SocketEventArgsTests;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace CustomNetworkLib
{
    public class ByteProtocolTcpServer : AsyncTcpServer
    {
        public bool V2 = false;
        public ByteProtocolTcpServer(int port): base(port)
        {}

        protected override IAsyncSession CreateSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            if(V2)
                return new ByteMessageSessionV2(e,sessionId);
            else
                return new ByteMessageSession(e,sessionId);
            // new ByteMessageSession(e,sessionId);
        }

        public void SendByteMessage(Guid clientGuid, byte[] rawByteMsg)
        {
            SendBytesToClient(clientGuid, rawByteMsg);
        }

        public void BroadcastByteMsg(byte[] rawByteMsg)
        {
            //byte[] framedMessage = BufferManager.AddByteFrame(rawByteMsg);
            SendBytesToAllClients(rawByteMsg);
        }
       
    }
}
