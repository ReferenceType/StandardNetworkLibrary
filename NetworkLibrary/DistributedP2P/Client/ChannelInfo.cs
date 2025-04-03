using NetworkLibrary.Components.Crypto;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkLibrary.DistributedP2P.Client
{
    public enum ChannelType
    {
        RawTcp,RawUdp,ByteMessage,SecureByteMessage
    }
    public class ChannelInfo
    {

        public ChannelType ChannelType { get; internal set; }

        public string ChannelName { get; internal set; }

        internal bool RequiresKeyExchange()
        {
            if(ChannelType == ChannelType.SecureByteMessage)
                return true;    
            return false;
        }
    }
}
