using JsonNetwork.Components;
using NetworkLibrary.MessageProtocol.Fast;
using System.Security.Cryptography.X509Certificates;

namespace JsonNetwork.MessageProtocol
{
    internal class SecureJsonMessageServer : GenericSecureMessageServerWrapper<JsonSerializer>
    {
        public SecureJsonMessageServer(int port, X509Certificate2 cerificate) : base(port, cerificate)
        {
        }
    }
}
