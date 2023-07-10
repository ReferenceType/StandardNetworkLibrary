using NetworkLibrary.P2P.Generic;
using Protobuff.Components.Serialiser;
using System.IO;
using System;
using System.Security.Cryptography.X509Certificates;
using NetworkLibrary.MessageProtocol;
using NetworkLibrary.P2P;

namespace Protobuff.P2P
{
    [Obsolete("SecureProtoRelayServer is obselete. Use NetworkLibrary.P2P.RelayServer")]
    public class SecureProtoRelayServer : SecureRelayServerBase<ProtoSerializer>
    {
       
        public SecureProtoRelayServer(int port, X509Certificate2 cerificate) : base(port, cerificate)
        {
        }
    }
}
