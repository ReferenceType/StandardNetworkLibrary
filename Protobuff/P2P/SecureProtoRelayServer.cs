using Protobuff.Components.Serialiser;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Protobuff.P2P
{
    public class SecureProtoRelayServer : SecureRelayServerBase<ProtoSerializer>
    {
        public SecureProtoRelayServer(int port, X509Certificate2 cerificate) : base(port, cerificate)
        {
        }
    }
}
