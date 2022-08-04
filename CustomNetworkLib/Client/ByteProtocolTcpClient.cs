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
        public bool V2=false;
        public ByteProtocolTcpClient()
        { }
       
        protected override void CreateSession(SocketAsyncEventArgs e,Guid sessionId)
        {
            //base.session = new ByteMessageSession(e, sessionId);
            if(V2)
                base.session = new ByteMessageSessionV2(e, sessionId);
            else
               base.session = new ByteMessageSession(e, sessionId);

        }

    }
}
