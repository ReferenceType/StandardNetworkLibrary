using NetSerializerNetwork.Components;
using NetworkLibrary.TCP.Generic;
using System.Security.Cryptography.X509Certificates;

namespace NetSerializerNetwork.Pure
{
    internal class SecureNetSerialiserClient : GenericSecureClient<NetSerialiser>
    {
        public SecureNetSerialiserClient(X509Certificate2 certificate, bool writeLenghtPrefix = true) : base(certificate, writeLenghtPrefix)
        {
        }
    }
}
