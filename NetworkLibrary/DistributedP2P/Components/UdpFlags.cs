using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkLibrary.DistributedP2P.Components
{
    [Flags]
    public enum UdpFlags:byte
    {
        StandardMessage = 1,
        JumboMessage = 2,
        ReliableMessage = 4,
        KeepAliveMessage = 8,
        HP = 16,
        HPAck = 32,
      
    }

}
