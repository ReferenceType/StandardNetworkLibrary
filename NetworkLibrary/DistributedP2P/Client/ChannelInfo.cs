using NetworkLibrary.Components.Crypto;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkLibrary.DistributedP2P.Client
{
    public enum ChannelType
    {
        RawTcp,RawUdp,ByteMessage,SecureByteMessage,UdpMessage,SecureUdpMessage
    }
    public class ChannelInfo
    {

        public ChannelType ChannelType { get; internal set; }

        public string ChannelName { get; internal set; }

        internal bool RequiresKeyExchange()
        {
            if(ChannelType == ChannelType.SecureByteMessage ||
                ChannelType == ChannelType.SecureUdpMessage)
                return true;    
            return false;
        }
    }
}
