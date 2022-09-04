using CustomNetworkLib.SocketEventArgsTests;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CustomNetworkLib
{
    public class ByteMessageTcpClient: AsyncTpcClient
    {
        public int MaxIndexedMemory = 128000;
        public bool DropOnCongestion = false;
        public ByteMessageTcpClient()
        { }
       
        protected override void CreateSession(SocketAsyncEventArgs e,Guid sessionId)
        {
            var session= new ByteMessageSession(e, sessionId);
            session.MaxIndexedMemory=MaxIndexedMemory;
            session.DropOnCongestion = DropOnCongestion;
            base.session = session;
            
        }



    }
}
