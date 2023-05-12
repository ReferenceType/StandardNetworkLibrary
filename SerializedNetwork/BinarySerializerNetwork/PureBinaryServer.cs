using BinarySerializerNetwork.Components;
using NetworkLibrary.Generic;
using System;
using System.Collections.Generic;
using System.Text;

namespace BinarySerializerNetwork
{
    internal class PureBinaryServer : GenericServer<BinarySerializer>
    {
        public PureBinaryServer(int port, bool writeLenghtPrefix = true) : base(port, writeLenghtPrefix)
        {
        }
    }
}
