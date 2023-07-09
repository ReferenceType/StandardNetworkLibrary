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

   
}
