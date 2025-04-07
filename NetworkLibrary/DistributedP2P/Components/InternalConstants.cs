using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkLibrary.DistributedP2P.Components
{
    internal class InternalConstants
    {
        public const string ConnectionStart = "0";
        public const string ConnectionReq = "1";
        public const string ConnectionAckGood = "2";
        public const string ConnectionAckBad = "3";
        public const string Error = "4";
        public const string ConnectionGetClientPublicData = "5";
        public const string ConnectionAckClientPublicData = "6";
        public const string PipeRequestTcp = "7";
        public const string PipeRequestUdp = "8";
        public const string PipeReqAck = "d";
        public const string PipeTokenDeliveryTcp = "9";
        public const string PipeTokenDeliveryUdp = "a";
        public const string PublishPeerList = "b";
        public const string SyncTime = "c";
        public const string RequestHolepunchUdp = "d";
        public const string AckRequestHolepunchUdp = "e";
        public const string StartHP = "f";
        public const string PunchSucces = "g";
        public const string PunchFail = "h";
        public const string PunchSuccesAck = "i";
        public const string PunchFailAck = "j";
        public const string RequestHolepunchTcp = "k";
        public const string AckRequestHolepunchTcp = "l";
    }
}
