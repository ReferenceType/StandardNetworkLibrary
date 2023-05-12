using MessagePackNetwork.Components;
using MessageProtocol;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace MessagePackNetwork
{
    internal class MessagePackMessageClientInternal : MessageClient<MessageEnvelope, MessagepackSerializer>
    {
       
    }
    internal class MessagePackMesssageServerInternal : MessageServer<MessageEnvelope, MessagepackSerializer>
    {
        public MessagePackMesssageServerInternal(int port) : base(port)
        {
        }

    }
}
