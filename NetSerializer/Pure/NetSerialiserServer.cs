using NetSerializerNetwork.Components;
using NetworkLibrary.TCP.Generic;

namespace NetSerializerNetwork.Pure
{
    public class NetSerialiserServer : GenericServer<NetSerialiser>
    {
        public NetSerialiserServer(int port, bool writeLenghtPrefix = true) : base(port, writeLenghtPrefix)
        {
        }
    }
}
