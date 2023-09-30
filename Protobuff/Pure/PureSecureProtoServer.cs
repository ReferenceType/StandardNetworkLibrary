using NetworkLibrary.TCP.Generic;
using Protobuff.Components.Serialiser;
using System.Security.Cryptography.X509Certificates;

namespace Protobuff.Pure
{
    public class PureSecureProtoServer : GenericSecureServer<ProtoSerializer>
    {
        public PureSecureProtoServer(int port, X509Certificate2 certificate, bool writeLenghtPrefix = true) : base(port, certificate, writeLenghtPrefix)
        {
        }
    }
}
