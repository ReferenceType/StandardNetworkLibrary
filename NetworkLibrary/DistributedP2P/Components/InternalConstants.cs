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
        public const string PipeTokenDeliveryTcp = "9";
        public const string PipeTokenDeliveryUdp = "a";
        public const string PublishPeerList = "b";
    }
}
