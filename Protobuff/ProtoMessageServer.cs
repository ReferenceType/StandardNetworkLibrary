using NetworkLibrary;
using NetworkLibrary.Components.Statistics;
using NetworkLibrary.MessageProtocol;
using NetworkLibrary.MessageProtocol.Fast;
using Protobuff.Components;
using Protobuff.Components.Serialiser;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Protobuff
{
    public class ProtoMessageServer : GenericMessageServerWrapper<ProtoSerializer>
    {
        public ProtoMessageServer(int port) : base(port)
        {
        }
    }
}

