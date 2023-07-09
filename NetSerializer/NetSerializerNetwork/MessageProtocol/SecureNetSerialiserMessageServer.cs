using NetSerializerNetwork.Components;
using NetworkLibrary.MessageProtocol.Fast;
using System.Security.Cryptography.X509Certificates;

namespace NetSerializerNetwork.MessageProtocol
{
    internal class SecureNetSerialiserMessageServer : GenericSecureMessageServerWrapper<NetSerialiser>
    {
        public SecureNetSerialiserMessageServer(int port, X509Certificate2 cerificate) : base(port, cerificate)
        {
        }
    }
}
