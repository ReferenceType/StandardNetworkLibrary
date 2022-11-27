using ProtoBuf;
using Protobuff.Components;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace Protobuff.P2P
{
    [ProtoContract]
    public class PeerInfo: IProtoMessage
    {
        [ProtoMember(1)]
        public string IP;
        [ProtoMember(2)]
        public int Port;
    }

    [ProtoContract]
    public class PeerList<T>: IProtoMessage
    {
        [ProtoMember(1)]
        public Dictionary<Guid,T> PeerIds;
        
    }
    public class RelayMessageResources
    {
        public const string UdpInit = "UdpInit";
        public const string UdpInitResend = "ResendUdpInit";
        public const string UdpFinaliseInit = "UdpFinaliseInit";
        public const string UdpRelayMessage = "UR";

        public const string NotifyClientInitComplete = "NotifyClientInitComplete";
        public const string NotifyPeerListUpdate = "PeerListUpdate";

        public const string RequestRegistery = "RequestRegistery";
        public const string RegisterySucces = "RegisterySucces";
        public const string RegisteryFail = "RegisteryFail";
        public const string RegisteryAck = "RegisteryAck";

        public const string HolePunchRequest = "HolePunchRequest";
        public const string RegisterHolePunchEndpoint = "SendFirstMsg";
        public const string EndpointTransfer = "EndpointTransfer";
        public const string HolePunchRegister = "HolePunchRegister";

        public static MessageEnvelope MakeRelayMessage(Guid fromId,Guid toId, byte[] payload)
        {
            var msg = new MessageEnvelope()
            {
                From = fromId,
                To= toId,
                Payload = payload
            };
            return msg;

        }

        public static MessageEnvelope MakeRelayRequestMessage(Guid requestId, Guid fromId, Guid toId, byte[] payload)
        {
            var msg = new MessageEnvelope()
            {
                MessageId=requestId,
                From = fromId,
                To = toId,
                Payload = payload
            };
            return msg;

        }

     


    }
}
