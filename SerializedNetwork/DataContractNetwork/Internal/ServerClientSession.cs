using DataContractNetwork.Components;
using MessageProtocol;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace DataContractNetwork.Internal
{
    internal class ServerClientSession
    {
        internal class DataContractClientInternal : MessageClient<MessageEnvelope, DataContractSerialiser>
        {
        }

        internal class DataContractServerIntenal : MessageServer<MessageEnvelope, DataContractSerialiser>
        {
            public DataContractServerIntenal(int port) : base(port)
            {
            }
        }

        internal class DataContractSesssionInternal : MessageSession<MessageEnvelope, DataContractSerialiser>
        {
            public DataContractSesssionInternal(SocketAsyncEventArgs acceptedArg, Guid sessionId) : base(acceptedArg, sessionId)
            {
            }
        }
    }
}
