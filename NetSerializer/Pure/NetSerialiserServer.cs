using NetSerializerNetwork.Components;
using NetworkLibrary.TCP.Generic;

namespace NetSerializerNetwork.Pure
{
    internal class NetSerialiserServer : GenericServer<NetSerialiser>
    {
        public NetSerialiserServer(int port, bool writeLenghtPrefix = true) : base(port, writeLenghtPrefix)
        {
        }
    }
}
