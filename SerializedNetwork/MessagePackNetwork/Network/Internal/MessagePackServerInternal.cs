using MessagePackNetwork.Network.Components;
using MessageProtocol;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace MessagePackNetwork.Network
{
    internal class MessagePackServerInternal : 
        MessageServer< MessageEnvelope, MessagepackSerializer>
    {
        public MessagePackServerInternal(int port) : base(port)
        {
        }

    }
}
