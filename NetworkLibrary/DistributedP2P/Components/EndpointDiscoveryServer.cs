using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NetworkLibrary.DistributedP2P.Channels;
using NetworkLibrary.P2P.Components.HolePunch;
using NetworkLibrary.TCP.Base;
using NetworkLibrary.Utils;
using NetworkLibrary.UDP;

namespace NetworkLibrary.DistributedP2P.Components
{
    internal class EndpointDiscoveryServer
    {

        AsyncTcpServer tcpServer;
        AsyncUdpServerLite udpServer;
        public EndpointDiscoveryServer(int port)
        {
            tcpServer = new AsyncTcpServer(port);
            tcpServer.OnClientAccepted += HandleTcpClient;
            udpServer =  new AsyncUdpServerLite(port);
            udpServer.OnBytesRecieved += HandleUdpClient;
        }

        private void HandleUdpClient(IPEndPoint remoteEp, byte[] bytes, int offset, int count)
        {
            try
            {
                var stream = SharerdMemoryStreamPool.RentStreamStatic();
                KnownTypeSerializer.SerializeEndpointData(stream, new EndpointData(remoteEp));
                udpServer.SendBytesToClient(remoteEp, stream.GetBuffer(), 0, stream.Position32);
            }
            catch { }
           
        }

        private void HandleTcpClient(Guid guid)
        {
            try
            {
                var remoteEp = tcpServer.GetSessionEndpoint(guid);

                var stream = SharerdMemoryStreamPool.RentStreamStatic();
                KnownTypeSerializer.SerializeEndpointData(stream, new EndpointData(remoteEp));
                tcpServer.SendBytesToClient(guid, stream.GetBuffer(), 0, stream.Position32);

                TimerService.RegisterTimer(guid, 1000, () =>
                {
                    tcpServer?.CloseSession(guid);
                });
            }
            catch { }
            
        }

        public void Start()
        {
            tcpServer.StartServer();
            udpServer.StartServer();
        }

        internal void Dispose()
        {
            try
            {
                tcpServer?.ShutdownServer();
                udpServer?.Dispose();
            }
            catch { }
        }
    }
}
