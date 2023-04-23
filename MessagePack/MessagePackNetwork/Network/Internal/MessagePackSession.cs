using MessagePackNetwork.Network.Components;
using MessageProtocol;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace MessagePackNetwork.Network
{
    internal class MessagePackSessionInternal : MessageSession<MessageEnvelope, MessagePackQueue>
    {
        public MessagePackSessionInternal(SocketAsyncEventArgs acceptedArg, Guid sessionId) : base(acceptedArg, sessionId)
        {
        }

        protected override MessagePackQueue GetMesssageQueue()
        {
            return new MessagePackQueue(MaxIndexedMemory, true);
        }
    }
}
