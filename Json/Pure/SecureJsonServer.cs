using JsonNetwork.Components;
using NetworkLibrary.TCP.Generic;
using System.Security.Cryptography.X509Certificates;

namespace JsonNetwork.Pure
{
    internal class SecureJsonServer : GenericSecureServer<JsonSerializer>
    {
        public SecureJsonServer(int port, X509Certificate2 certificate, bool writeLenghtPrefix = true) : base(port, certificate, writeLenghtPrefix)
        {
        }
    }
}
