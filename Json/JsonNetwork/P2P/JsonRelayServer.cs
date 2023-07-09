using JsonNetwork.Components;
using NetworkLibrary.P2P.Generic;
using System.Security.Cryptography.X509Certificates;

namespace JsonNetwork.P2P
{
    internal class JsonRelayServer : SecureRelayServerBase<JsonSerializer>
    {
        public JsonRelayServer(int port, X509Certificate2 cerificate) : base(port, cerificate)
        {
        }
    }
}
