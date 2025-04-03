using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using NetworkLibrary.DistributedP2P.Client;


namespace NetworkLibrary.DistributedP2P.Channels
{
    public class RawUdpSocket:IChannel
    {
        public Socket socket;
        public RawUdpSocket(ChannelInfo info,Socket socket )
        {
            this.socket = socket;
            Info = info;
        }

        public ChannelInfo Info { get; }

    }
}
