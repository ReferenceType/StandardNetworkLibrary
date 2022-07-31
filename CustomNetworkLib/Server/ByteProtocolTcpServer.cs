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
        
        public ByteProtocolTcpServer(int port): base(port)
        {}

        protected override IAsyncSession CreateSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            return new ByteMessageSession(e,sessionId);
        }

        public void SendByteMessage(Guid clientGuid, byte[] rawByteMsg)
        {
            byte[] framedMessage = BufferManager.AddByteFrame(rawByteMsg);
            SendBytesToClient(clientGuid, framedMessage);
        }

        public void BroadcastByteMsg(byte[] rawByteMsg)
        {
            //byte[] framedMessage = BufferManager.AddByteFrame(rawByteMsg);
            SendBytesToAllClients(rawByteMsg);
        }
       
    }
}
