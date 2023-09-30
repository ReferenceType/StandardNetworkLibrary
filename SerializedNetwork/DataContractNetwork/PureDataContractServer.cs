using DataContractNetwork.Components;
using NetworkLibrary.TCP.Generic;

namespace DataContractNetwork
{
    internal class PureDataContractServer : GenericServer<DataContractSerialiser>
    {
        public PureDataContractServer(int port, bool writeLenghtPrefix = true) : base(port, writeLenghtPrefix)
        {
        }
    }
}
