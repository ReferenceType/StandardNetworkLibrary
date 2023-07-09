using NetSerializerNetwork.Components;
using NetworkLibrary.P2P.Generic.Room;
using System.Security.Cryptography.X509Certificates;

namespace NetSerializerNetwork.P2P.Room
{
    internal class NetSerialiserRoomClient : SecureLobbyClient<NetSerialiser>
    {
        public NetSerialiserRoomClient(X509Certificate2 clientCert) : base(clientCert)
        {
        }
    }
}
