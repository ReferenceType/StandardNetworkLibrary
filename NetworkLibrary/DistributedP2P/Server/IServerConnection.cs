using NetworkLibrary.DistributedP2P.Components;
using NetworkLibrary.DistributedP2P.Server.StateManagement;
using NetworkLibrary.P2P.Components.HolePunch;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NetworkLibrary.DistributedP2P.Server
{
    internal interface IServerConnection : IDistributedConnection
    {
        void GetPipeToken(bool isTcpPipe, Guid fromEphemeral, Guid toEphemeral, Action<PipeResult> onReady);
    }
}
