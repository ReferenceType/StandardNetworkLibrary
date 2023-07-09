using DataContractNetwork.Components;
using MessageProtocol;
using System;
using System.Net.Sockets;

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

       
    }
}
