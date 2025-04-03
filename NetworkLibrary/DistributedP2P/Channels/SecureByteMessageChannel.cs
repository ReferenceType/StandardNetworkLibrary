using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetworkLibrary.DistributedP2P.Client;
using NetworkLibrary.TCP.AES;

namespace NetworkLibrary.DistributedP2P.Channels
{
    public class SecureByteMessageChannel : IChannel
    {
        AesTcpClient client;
        AesTcpServer server;
        private Guid clientId;
        bool clientMode = false;

        public ChannelInfo Info { get; private set; }

        public event Action<byte[], int, int> BytesReceived;
        public event Action Disconnected;

        public SecureByteMessageChannel(AesTcpClient client, ChannelInfo info)
        {
            this.client = client;
            Info = info;

            clientMode = true;
            client.OnBytesReceived += ClientBytesReceived;
            client.OnDisconnected += ClientDisconnected;
        }

        public SecureByteMessageChannel(AesTcpServer server, ChannelInfo info)
        {
            this.server = server;
            Info = info;

            clientId = server.Sessions.First().Key;
            server.OnBytesReceived += ServerBytesReceived;
            server.OnClientDisconnected += ServerClientDisconnected;
        }


        private void ServerBytesReceived(Guid guid, byte[] bytes, int offset, int count)
        {
            ClientBytesReceived(bytes, offset, count);
        }

        private void ClientBytesReceived(byte[] bytes, int offset, int count)
        {
            BytesReceived?.Invoke(bytes, offset, count);
        }


        public void SendAsync(byte[] buffer, int offset, int count)
        {
            if (clientMode)
                client.SendAsync(buffer, offset, count);
            else
                server.SendBytesToClient(clientId, buffer, offset, count);
        }


        private void ServerClientDisconnected(Guid guid)
        {
            ClientDisconnected();
        }


        private void ClientDisconnected()
        {
            Disconnected?.Invoke();
        }

        public void Start()
        {
            if (clientMode)
            {
                client.Start();
            }            
        }
    }
}
