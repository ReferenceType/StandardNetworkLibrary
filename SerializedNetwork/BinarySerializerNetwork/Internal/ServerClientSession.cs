using BinarySerializerNetwork.Components;
using MessageProtocol;
using System;
using System.Net.Sockets;

namespace BinarySerializerNetwork.Internal
{
    internal class ServerClientSession
    {
        internal class NetSerializerClientInternal : MessageClient<MessageEnvelope, BinarySerializer>
        {
        }

        internal class NetSerializerServerIntenal : MessageServer<MessageEnvelope, BinarySerializer>
        {
            public NetSerializerServerIntenal(int port) : base(port)
            {
            }
        }

     
    }
}
