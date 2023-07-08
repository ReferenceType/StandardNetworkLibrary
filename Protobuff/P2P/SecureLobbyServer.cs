using NetworkLibrary.P2P.Generic.Room;
using Protobuff.Components.Serialiser;
using System.Security.Cryptography.X509Certificates;

namespace Protobuff.P2P
{
    public class SecureLobbyServer : SecureLobbyServer<ProtoSerializer>
    {
        public SecureLobbyServer(int port, X509Certificate2 cerificate) : base(port, cerificate)
        {
        }
    }
}
