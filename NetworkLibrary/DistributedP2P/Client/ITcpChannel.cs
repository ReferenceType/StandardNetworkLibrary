using System;

namespace NetworkLibrary.DistributedP2P.Client
{

    public interface ITcpChannel
    {
        ChannelInfo Info { get; }

        event Action<byte[], int, int> BytesReceived;
        event Action Disconnected;
        void SendAsync(byte[] buffer, int offset, int count);
    }
}