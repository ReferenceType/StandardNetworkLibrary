using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkLibrary.DistributedP2P.Components
{
    internal class InternalConstants
    {
        public const string ConnectionStart = "-";
        public const string ConnectionReq = "0";
        public const string ConnectionAckGood = "1";
        public const string Error = "2";
        public const string ConnectionGetClientPublicData = "3";
        public const string ConnectionAckClientPublicData = "4";
    }
}
