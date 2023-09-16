using NetworkLibrary.P2P.Generic;
using Protobuff.Components.Serialiser;
using System.Security.Cryptography.X509Certificates;


namespace Protobuff.P2P
{
    public class RelayClient : RelayClientBase<ProtoSerializer>
    {
        public RelayClient(X509Certificate2 clientCert, int udpPort = 0) : base(clientCert,udpPort)
        {
        }
    }
}
