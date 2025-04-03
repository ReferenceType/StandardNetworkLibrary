using NetworkLibrary.DistributedP2P.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Sockets;
using System.Text;

namespace NetworkLibrary.DistributedP2P.Channels
{
    public class RawTcpSocket : IChannel
    {
        public Socket socket;
        public RawTcpSocket(ChannelInfo info, Socket socket)
        {
            this.socket = socket;
            Info = info;
        }

        public ChannelInfo Info { get; }

    }
}
