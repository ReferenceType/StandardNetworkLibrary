using NetworkLibrary.Components.Crypto;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkLibrary.DistributedP2P.Client
{
    public enum ChannelType
    {
        Tcp,SecureTcp,Udp,SecureUdp
    }

    public enum TcpHolePunchStrategy
    {
       Sequential, Simultaneous
    }
    public class ChannelInfo
    {
        public ChannelInfo()
        {
        }

        public ChannelInfo(ChannelType channelType, string channelName)
        {
            ChannelType = channelType;
            ChannelName = channelName;
        }

        public ChannelType ChannelType { get; internal set; }

        public string ChannelName { get; internal set; } = "";

        internal bool RequiresKeyExchange()
        {
            if(ChannelType == ChannelType.SecureTcp ||
                ChannelType == ChannelType.SecureUdp)
                return true;    
            return false;
        }
    }
}
