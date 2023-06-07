using ProtoBuf;
using System;
using System.Collections.Generic;

namespace Protobuff.P2P
{
    [ProtoContract]
    public class PeerList : IProtoMessage
    {
        [ProtoMember(1)]
        public Dictionary<Guid, PeerInfo> PeerIds { get; set; }

    }

    [ProtoContract]
    public class PeerInfo : IProtoMessage
    {
        [ProtoMember(1)]
        public byte[] Address { get; set; }
        [ProtoMember(2)]
        public ushort Port { get; set; }
    }
}
