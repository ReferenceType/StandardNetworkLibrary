using NetworkLibrary.MessageProtocol;
using System;
using System.Net.Sockets;

namespace CustomSerializer
{
    public class CustomMessageClient : MessageClient<Serializer_>
    {

    }

    public class CustomMessageServer : MessageServer<Serializer_>
    {
        public CustomMessageServer(int port) : base(port)
        {
        }
    }

    public class CustomMessageSession : MessageSession<Serializer_>
    {
        public CustomMessageSession(SocketAsyncEventArgs acceptedArg, Guid sessionId) : base(acceptedArg, sessionId)
        {
        }
    }
}
