using NetworkLibrary.DistributedP2P.Client;
using NetworkLibrary.TCP.ByteMessage;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace NetworkLibrary.DistributedP2P.Channels
{
    public class ByteMessageChannel : ITcpChannel
    {
        public ChannelInfo Info { get; private set; }

        public event Action<byte[], int, int> BytesReceived;
        public event Action Disconnected;

        private ByteMessageTcpClient client;
        private readonly Socket connectedSocket;

        public ByteMessageChannel(ChannelInfo info, Socket connectedSocket)
        {
            Info = info;
            this.connectedSocket = connectedSocket;
            client = new ByteMessageTcpClient();
            client.GatherConfig = ScatterGatherConfig.UseBuffer;

            client.OnDisconnected += () => Disconnected?.Invoke();
            client.OnBytesReceived += (b, o, c) => BytesReceived?.Invoke(b, o, c);
        }

        public void Start()
        {
            client.SetConnectedSocket(connectedSocket, ScatterGatherConfig.UseBuffer);
        }


        public void SendAsync(byte[] buffer, int offset, int count)
        {
            client.SendAsync(buffer, offset, count);
        }

        
    }
}
