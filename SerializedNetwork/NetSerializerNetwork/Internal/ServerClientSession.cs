using MessageProtocol;
using NetSerializerNetwork.Components;
using System;
using System.Net.Sockets;

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

   
}
