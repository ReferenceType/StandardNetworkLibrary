using NetSerializerNetwork.Components;
using NetworkLibrary.P2P.Generic;
using System.Security.Cryptography.X509Certificates;

namespace NetSerializerNetwork.P2P
{
    internal class NetSerialiserRelayClient : RelayClientBase<NetSerialiser>
    {
        public NetSerialiserRelayClient(X509Certificate2 clientCert) : base(clientCert)
        {
        }
    }
}
