using NetSerializerNetwork.Components;
using NetworkLibrary.Generic;

namespace NetSerializerNetwork.Pure
{
    internal class NetSerialiserServer : GenericServer<NetSerialiser>
    {
        public NetSerialiserServer(int port, bool writeLenghtPrefix = true) : base(port, writeLenghtPrefix)
        {
        }
    }
}
