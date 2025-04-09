using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkLibrary.DistributedP2P.Channels.Components
{
    public enum MessageFlags : byte
    {
        StandardMessage,
        JumboMessage,
        ReliableMessage,
        InternalReliableMessage,
        KeepAliveMessage,
        HP,
        HPAck,
        Ping,
        Pong,
        KeyExchange,
        KeyExchangeAck,
        KeyExchangeFin,
        Kill,
    }

    

}
