using MessagePackNetwork.Network.Components;
using MessageProtocol;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace MessagePackNetwork.Network
{
    internal class MessagePackSessionInternal : MessageSession<MessageEnvelope, MessagepackSerializer>
    {
        public MessagePackSessionInternal(SocketAsyncEventArgs acceptedArg, Guid sessionId) : base(acceptedArg, sessionId)
        {

        }

      
    }
}
