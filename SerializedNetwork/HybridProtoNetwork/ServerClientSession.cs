using MessageProtocol;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace CustomSerializer
{
   public class CustomMessageClient : NetworkLibrary.MessageProtocol.Fast.MessageClient<Serializer_>
    {

    }

    public class CustomMessageServer : NetworkLibrary.MessageProtocol.Fast.MessageServer<Serializer_>
    {
        public CustomMessageServer(int port) : base(port)
        {
        }
    }

    public class CustomMessageSession : NetworkLibrary.MessageProtocol.Fast.MessageSession<Serializer_>
    {
        public CustomMessageSession(SocketAsyncEventArgs acceptedArg, Guid sessionId) : base(acceptedArg, sessionId)
        {
        }
    }
}
