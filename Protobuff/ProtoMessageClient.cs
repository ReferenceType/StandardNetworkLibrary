using NetworkLibrary;
using NetworkLibrary.Components.Statistics;
using NetworkLibrary.MessageProtocol;
using NetworkLibrary.MessageProtocol.Fast;
using Protobuff.Components;
using Protobuff.Components.Serialiser;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Protobuff
{
  
    public class ProtoMessageClient:GenericMessageClientWrapper<ProtoSerializer>
    {

    }
}
