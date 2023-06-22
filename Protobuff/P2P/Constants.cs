using NetworkLibrary;
using NetworkLibrary.MessageProtocol.Serialization;
using ProtoBuf;

namespace Protobuff.P2P
{
   
    public class Constants
    {
        public const string Register = "1";
        public const string ServerRegisterAck = "2";
        public const string UdpInit = "3";
        public const string ServerFinalizationCmd = "4";
        public const string ClientFinalizationAck = "5";
        public const string NotifyPeerListUpdate = "6";

        //hp 
        public const string InitiateHolepunch = "7";
        public const string HolePunchSucces = "8";
        public const string KeyTransfer = "9";
        public const string HolpunchMessagesSent = "10";
        public const string NotifyServerHolepunch = "11";

        public const string Ping = "Ping";
        public const string Pong = "Pong";


        public const string RoomUpdate = "12";
        public const string JoinRoom = "13";
        public const string LeaveRoom = "14";
        public const string GetAvailableRooms = "15";
        public const string PeerDisconnected = "16";
        public const string Rudp = "17";

        public const string RoomName = "42";
    }
}
