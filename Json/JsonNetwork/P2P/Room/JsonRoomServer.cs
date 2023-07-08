using JsonNetwork.Components;
using NetworkLibrary.P2P.Generic.Room;
using System.Security.Cryptography.X509Certificates;

namespace JsonNetwork.P2P.Room
{
    internal class JsonRoomServer : SecureLobbyServer<JsonSerializer>
    {
        public JsonRoomServer(int port, X509Certificate2 cerificate) : base(port, cerificate)
        {
        }
    }
}
