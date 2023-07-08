using NetSerializerNetwork.Components;
using NetworkLibrary.P2P.Generic;
using System.Security.Cryptography.X509Certificates;

namespace NetSerializerNetwork.P2P
{
    internal class NetSerialiserRelayServer : SecureRelayServerBase<NetSerialiser>
    {
        public NetSerialiserRelayServer(int port, X509Certificate2 cerificate) : base(port, cerificate)
        {
        }
    }
}
