using System;

namespace NetworkLibrary.DistributedP2P.Client
{

    public interface IChannel
    {
        ChannelInfo Info { get; }
        
    }
}