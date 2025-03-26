using NetworkLibrary.Components.Crypto;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkLibrary.DistributedP2P.Client
{
    public class ChannelInfo
    {
        public AesMode AesMode { get; }

        public string ChannelName { get; }
    }
}
