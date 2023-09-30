using NetSerializerNetwork.Components;
using NetworkLibrary.MessageProtocol.Fast;
using System.Security.Cryptography.X509Certificates;

namespace NetSerializerNetwork.MessageProtocol
{
    internal class SecureNetSerialiserMessageClient : GenericSecureMessageClientWrapper<NetSerialiser>
    {
        public SecureNetSerialiserMessageClient(X509Certificate2 certificate) : base(certificate)
        {
        }
    }
}
