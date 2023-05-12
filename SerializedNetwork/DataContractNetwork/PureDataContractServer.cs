using DataContractNetwork.Components;
using NetworkLibrary.Generic;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataContractNetwork
{
    internal class PureDataContractServer : GenericServer<DataContractSerialiser>
    {
        public PureDataContractServer(int port, bool writeLenghtPrefix = true) : base(port, writeLenghtPrefix)
        {
        }
    }
}
