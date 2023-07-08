using BinarySerializerNetwork.Components;
using NetworkLibrary.Generic;

namespace BinarySerializerNetwork
{
    internal class PureBinaryServer : GenericServer<BinarySerializer>
    {
        public PureBinaryServer(int port, bool writeLenghtPrefix = true) : base(port, writeLenghtPrefix)
        {
        }
    }
}
