using NetworkLibrary.P2P.Generic;
using Protobuff.Components.Serialiser;
using System.Security.Cryptography.X509Certificates;

namespace Protobuff.P2P
{
    public class SecureProtoRelayServer : SecureRelayServerBase<ProtoSerializer>
    {
        public SecureProtoRelayServer(int port, X509Certificate2 cerificate) : base(port, cerificate)
        {
        }
    }
}
