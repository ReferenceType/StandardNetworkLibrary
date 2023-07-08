using NetSerializerNetwork.Components;
using NetworkLibrary.P2P.Generic.Room;
using System.Security.Cryptography.X509Certificates;

namespace NetSerializerNetwork.P2P.Room
{
    internal class NetSerialiserRoomServer : SecureLobbyServer<NetSerialiser>
    {
        public NetSerialiserRoomServer(int port, X509Certificate2 cerificate) : base(port, cerificate)
        {
        }
    }
}
