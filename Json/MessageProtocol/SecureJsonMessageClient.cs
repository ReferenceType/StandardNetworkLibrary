using JsonNetwork.Components;
using NetworkLibrary.MessageProtocol.Fast;
using System.Security.Cryptography.X509Certificates;

namespace JsonNetwork.MessageProtocol
{
    public class SecureJsonMessageClient : GenericSecureMessageClientWrapper<JsonSerializer>
    {
        public SecureJsonMessageClient(X509Certificate2 certificate) : base(certificate)
        {
        }
    }
}
