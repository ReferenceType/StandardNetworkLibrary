using ProtoBuf;
using System;
using System.Collections.Generic;

namespace Protobuff.P2P.Generic
{
    public class PeerList : IProtoMessage
    {
        public Dictionary<Guid, PeerInfo> PeerIds { get; set; }
    }

    public class PeerInfo : IProtoMessage
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
