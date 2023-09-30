using BinarySerializerNetwork.Components;
using NetworkLibrary.TCP.Generic;

namespace BinarySerializerNetwork
{
    internal class PureBinaryServer : GenericServer<BinarySerializer>
    {
        public PureBinaryServer(int port, bool writeLenghtPrefix = true) : base(port, writeLenghtPrefix)
        {
        }
    }
}
