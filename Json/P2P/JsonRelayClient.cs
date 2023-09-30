using JsonNetwork.Components;
using NetworkLibrary.P2P.Generic;
using System.Security.Cryptography.X509Certificates;

namespace JsonNetwork.P2P
{
    internal class JsonRelayClient : RelayClientBase<JsonSerializer>
    {
        public JsonRelayClient(X509Certificate2 clientCert) : base(clientCert)
        {
        }
    }
}
