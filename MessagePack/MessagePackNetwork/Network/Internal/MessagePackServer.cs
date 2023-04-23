using MessagePackNetwork.Network.Components;
using MessageProtocol;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace MessagePackNetwork.Network
{
    internal class MessagePackServerInternal : 
        MessageServer<MessagePackQueue, MessageEnvelope, GenericMessageSerializer<MessageEnvelope,MessagePackSerializer_>>
    {
        public MessagePackServerInternal(int port) : base(port)
        {
        }

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
