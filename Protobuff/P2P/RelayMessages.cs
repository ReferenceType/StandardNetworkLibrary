using ProtoBuf;
using System;
using System.Collections.Generic;

namespace Protobuff.P2P
{
    [ProtoContract]
    public class PeerInfo : IProtoMessage
    {
        [ProtoMember(1)]
        public string IP;
        [ProtoMember(2)]
        public int Port;
    }

    [ProtoContract]
    public class PeerList<T> : IProtoMessage
    {
        [ProtoMember(1)]
        public Dictionary<Guid, T> PeerIds;

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
