using MessageProtocol;
using NetSerializerNetwork.Components;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace NetSerializerNetwork.Internal
{
    internal class NetSerializerClientInternal : MessageClient<MessageEnvelope, NetSerialiser>
    {
    }

    internal class NetSerializerServerIntenal : MessageServer<MessageEnvelope, NetSerialiser>
    {
        public NetSerializerServerIntenal(int port) : base(port)
        {
        }
    }

    internal class NetSerializerSesssionInternal : MessageSession<MessageEnvelope, NetSerialiser>
    {
        public NetSerializerSesssionInternal(SocketAsyncEventArgs acceptedArg, Guid sessionId) : base(acceptedArg, sessionId)
        {
        }
    }
}
