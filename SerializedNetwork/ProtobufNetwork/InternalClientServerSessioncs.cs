using MessageProtocol;
using System;
using System.Net.Sockets;

namespace ProtobufNetwork
{
    internal class ProtoClientInternal :
         MessageClient<MessageEnvelope, ProtoSerializer>
    {

    }
    internal class ProtoServerInternal : MessageServer<MessageEnvelope, ProtoSerializer>
    {
        internal ProtoServerInternal(int port) : base(port)
        {
        }
    }
    internal class ProtoSessionInternal : MessageSession<MessageEnvelope, ProtoSerializer>
    {
        public ProtoSessionInternal(SocketAsyncEventArgs acceptedArg, Guid sessionId) : base(acceptedArg, sessionId)
        {
        }
    }
}
