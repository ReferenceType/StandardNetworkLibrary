using System.Collections.Generic;
using System.Net;

namespace NetworkLibrary.P2P.Components.HolePunch
{

    public class EndpointTransferMessage
    {
        public byte[] IpRemote { get; set; }
        public int PortRemote { get; set; }
        public List<EndpointData> LocalEndpoints { get; set; } = new List<EndpointData>();
    }


    public class EndpointData
    {
        public byte[] Ip;
        public int Port;

        public IPEndPoint ToIpEndpoint()
        {
            return new IPEndPoint(new IPAddress(Ip), Port);
        }

        public EndpointData()
        {

        }

        public EndpointData(IPEndPoint ep)
        {
            Ip = ep.Address.MapToIPv4().GetAddressBytes();
            Port = ep.Port;
        }

        public EndpointData(string ip, int port)
        {
            IPAddress add = IPAddress.Parse(ip);
            Ip = add.GetAddressBytes();
            this.Port = port;
        }
    }
}
