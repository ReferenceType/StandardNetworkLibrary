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
    public class ByteProtocolTcpClient: AsyncTpcClient
    {
        public ByteProtocolTcpClient()
        { }
       
        protected override void CreateSession(SocketAsyncEventArgs e,Guid sessionId)
        {
            base.session = new ByteMessageSession(e, sessionId);
        }   
       
    }
}
