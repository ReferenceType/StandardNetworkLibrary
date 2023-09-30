using JsonNetwork.Components;
using NetworkLibrary.TCP.Generic;
using System.Security.Cryptography.X509Certificates;

namespace JsonNetwork.Pure
{
    internal class SecureJsonClient : GenericSecureClient<JsonSerializer>
    {
        public SecureJsonClient(X509Certificate2 certificate, bool writeLenghtPrefix = true) : base(certificate, writeLenghtPrefix)
        {
        }
    }
}
