using MessageProtocol;
using NetSerializerNetwork.Components;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace NetSerializerNetwork.Internal
{
    internal class NetSerializerClientInternal : MessageClient<MessageEnvelope, NetSerializer_>
    {
    }

    internal class NetSerializerServerIntenal : MessageServer<MessageEnvelope, NetSerializer_>
    {
        public NetSerializerServerIntenal(int port) : base(port)
        {
        }
    }

    internal class NetSerializerSesssionInternal : MessageSession<MessageEnvelope, NetSerializer_>
    {
        public NetSerializerSesssionInternal(SocketAsyncEventArgs acceptedArg, Guid sessionId) : base(acceptedArg, sessionId)
        {
        }
    }
}
