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

        internal class NetSerializerSesssionInternal : MessageSession<MessageEnvelope, BinarySerializer>
        {
            public NetSerializerSesssionInternal(SocketAsyncEventArgs acceptedArg, Guid sessionId) : base(acceptedArg, sessionId)
            {
            }
        }
    }
}
