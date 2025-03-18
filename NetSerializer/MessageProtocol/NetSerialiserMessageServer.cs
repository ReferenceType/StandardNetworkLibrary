using NetSerializerNetwork.Components;
using NetworkLibrary.MessageProtocol.Fast;

namespace NetSerializerNetwork.MessageProtocol
{
    public class NetSerialiserMessageServer : GenericMessageServerWrapper<NetSerialiser>
    {
        public NetSerialiserMessageServer(int port) : base(port)
        {
        }
    }
}
