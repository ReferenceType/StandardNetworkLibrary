using NetworkLibrary;
using NetworkLibrary.MessageProtocol.Serialization;
using ProtoBuf;

namespace Protobuff.P2P
{
   
    public class Constants
    {
        public const string Register = "1";
        public const string ServerCmd = "2";
        public const string UdpInit = "3";
        public const string ServerFinalizationCmd = "4";
        public const string ClientFinalizationAck = "5";
        public const string NotifyPeerListUpdate = "6";
        //hp 2
        public const string RequestHopepunch = "7";
        public const string EndpointTransfer = "8";
        public const string RequestHopepunchAck = "9";
        public const string HolePunchSucces = "10";
        public const string KeyTransfer = "11";

        // holepunch related
        public const string CreateChannel = "12";
        public const string HoplePunch = "13";
        public const string HoplePunchUdpResend = "14";

        public const string HolePunchRequest = "15";
        public const string SuccesAck = "16";
        public const string SuccessFinalize = "17";

        public const string Ping = "Ping";
        public const string Pong = "Pong";
        public const string GetEndpoints = "18";
        public const string HolpunchMessagesSent="19";

        public const byte DefaultUdpMsg = 0xFF;
    }
}
