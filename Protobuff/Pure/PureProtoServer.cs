using NetworkLibrary.Generic;
using Protobuff.Components.Serialiser;

namespace Protobuff.Pure
{
    public class PureProtoServer : GenericServer<ProtoSerializer>
    {
        public PureProtoServer(int port, bool writeLenghtPrefix = true) : base(port, writeLenghtPrefix)
        {
        }
    }
}
