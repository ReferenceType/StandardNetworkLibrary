using System;
using System.Collections.Generic;

namespace NetworkLibrary.P2P.Components
{
    public class PeerList
    {
        public Dictionary<Guid, PeerInfo> PeerIds { get; set; }
    }

    public class PeerInfo
    {
        public byte[] Address { get; set; }
        public ushort Port { get; set; }
    }

    public class RoomPeerList
    {
        public string RoomName { get; set; }
        public PeerList Peers;

    }


}
