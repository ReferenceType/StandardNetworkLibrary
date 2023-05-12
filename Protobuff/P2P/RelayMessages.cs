using NetworkLibrary;
using NetworkLibrary.MessageProtocol.Serialization;
using ProtoBuf;
using System;
using System.Collections.Generic;

namespace Protobuff.P2P
{
    [ProtoContract]
    public class PeerInfo : IProtoMessage
    {
        [ProtoMember(1)]
        public byte[] Address { get; set; }
        [ProtoMember(2)]
        public ushort Port { get; set; }
    }

    [ProtoContract]
    public class PeerList : IProtoMessage
    {
        [ProtoMember(1)]
        public Dictionary<Guid, PeerInfo> PeerIds { get; set; }

    }
    public class InternalMessageResources
    {
        public const string UdpInit = "%1";
        public const string UdpInitResend = "%2";
        public const string UdpFinaliseInit = "%3";

        public const string NotifyClientInitComplete = "%5";
        public const string NotifyPeerListUpdate = "%6";

        public const string RequestRegistery = "%7";
        public const string RegisterySucces = "%8";
        public const string RegisteryFail = "%9";
        public const string RegisteryAck = "%%";



        public static MessageEnvelope MakeRelayMessage(Guid fromId, Guid toId, byte[] payload)
        {
            var msg = new MessageEnvelope()
            {
                From = fromId,
                To = toId,
                Payload = payload
            };
            return msg;

        }

        public static MessageEnvelope MakeRelayRequestMessage(Guid requestId, Guid fromId, Guid toId, byte[] payload)
        {
            var msg = new MessageEnvelope()
            {
                MessageId = requestId,
                From = fromId,
                To = toId,
                Payload = payload
            };
            return msg;

        }




    }
}
