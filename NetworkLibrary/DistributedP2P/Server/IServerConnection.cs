using NetworkLibrary.DistributedP2P.Components;
using NetworkLibrary.DistributedP2P.Server.StateManagement;
using NetworkLibrary.P2P.Components.HolePunch;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NetworkLibrary.DistributedP2P.SimpleRelay;

namespace NetworkLibrary.DistributedP2P.Server
{
    internal interface IServerConnection : IDistributedConnection
    {
        void GetPipeToken(bool isTcpPipe, Guid fromEphemeral, Guid toEphemeral, Action<PipeResult> onReady);
        RoomResult CreateOrJoinRoom(string roomName, string roomPassword, Guid peerId, RoomProtocol protocol);
        void EndSession(Guid ephemeralId);
    }
}
