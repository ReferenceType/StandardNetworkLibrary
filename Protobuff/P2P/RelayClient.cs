using NetworkLibrary.Components;
using NetworkLibrary.Components.Statistics;
using NetworkLibrary;
using NetworkLibrary.MessageProtocol.Serialization;
using NetworkLibrary.Utils;
using Protobuff.Components;
using Protobuff.P2P.HolePunch;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Net;
using NetworkLibrary.UDP;
using System.Net.Sockets;
using NetworkLibrary.MessageProtocol;
using System.Threading;
using Protobuff.P2P.StateManagemet;
using System.Diagnostics;
using System.Runtime.InteropServices.ComTypes;
using Protobuff.P2P.Modules;
using Protobuff.Components.Serialiser;

namespace Protobuff.P2P
{
    public class RelayClient : RelayClientBase<ProtoSerializer>
    {
        public RelayClient(X509Certificate2 clientCert) : base(clientCert)
        {
        }
    }
}
