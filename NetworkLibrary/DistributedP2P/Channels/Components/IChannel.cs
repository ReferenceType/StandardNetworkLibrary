using System;
using NetworkLibrary.DistributedP2P.Client;

namespace NetworkLibrary.DistributedP2P.Channels.Components
{

    public interface IChannel
    {
        ChannelInfo Info { get; }

        void Start();

        void CloseChannel();

    }
}