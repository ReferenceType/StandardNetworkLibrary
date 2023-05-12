using NetworkLibrary.Generic;
using Protobuff.Components.Serialiser;
using System;
using System.Collections.Generic;
using System.Text;

namespace Protobuff.Pure
{
    internal class PureProtoServer : GenericServer<ProtoSerializer>
    {
        public ProtoSerializer Serializer = new ProtoSerializer();
        public PureProtoServer(int port, bool writeLenghtPrefix = true) : base(port, writeLenghtPrefix)
        {
        }
    }
}
