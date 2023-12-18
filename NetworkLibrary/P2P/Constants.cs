namespace NetworkLibrary.P2P
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
        public const string Rudp1 = "18";
        public const string Rudp2 = "19";
        public const string Judp = "20";

        // key of a dict, picked some unusual control char.
        public const string RoomName = "\v";

        public const string ReqTCPHP = "ReqTCPHP";
        public const string TcpPortMap = "TcpPortMap";
        public const string OkSendUdp = "OkSendUdp";
        public const string ResendUdp = "ResendUdp";
        public const string AckPortMap = "AckPortMap";
        public const string TryConnect = "TryConnect";
        public const string SwapToClient = "SwapToClient";
        public const string FinalizeSuccess = "FinalizeSuccess";
        public const string FinalizeFail = "FinalizeFail";
        public const string Failed = "Failed";
        public const string Success = "Success";
        public const string InitTCPHPRemote = "InitTCPHPRemote";

    }
}
