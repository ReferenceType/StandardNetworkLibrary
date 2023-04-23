using MessagePackNetwork.Network.Components;
using MessageProtocol;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace MessagePackNetwork.Network
{
    internal class MessagePackClientInternal : MessageClient<MessagePackQueue, MessageEnvelope, GenericMessageSerializer<MessageEnvelope,MessagePackSerializer_>>
    {
        protected override GenericMessageSerializer<MessageEnvelope, MessagePackSerializer_> CreateMessageSerializer()
        {
            return new MessagePackMessageSerializer();
        }

        protected override MessageSession<MessageEnvelope, MessagePackQueue> MakeSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            return new MessagePackSessionInternal(e, sessionId);
        }
    }
}
