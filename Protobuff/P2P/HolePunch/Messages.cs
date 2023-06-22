using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Net;

namespace Protobuff.P2P.HolePunch
{
    
    internal class EndpointTransferMessage : IProtoMessage
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
            Ip = ep.Address.GetAddressBytes();
            Port = ep.Port;
        }
    }
}
