using NetSerializerNetwork.Components;
using NetworkLibrary.TCP.Generic;
using System.Security.Cryptography.X509Certificates;

namespace NetSerializerNetwork.Pure
{
    internal class SecureNetSerialiserServer : GenericSecureServer<NetSerialiser>
    {
        public SecureNetSerialiserServer(int port, X509Certificate2 certificate, bool writeLenghtPrefix = true) : base(port, certificate, writeLenghtPrefix)
        {
        }
    }
}
