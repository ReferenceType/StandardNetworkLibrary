using JsonNetwork.Components;
using NetworkLibrary.P2P.Generic.Room;
using System.Security.Cryptography.X509Certificates;

namespace JsonNetwork.P2P.Room
{
    internal class JsonRoomClient : SecureLobbyClient<JsonSerializer>
    {
        public JsonRoomClient(X509Certificate2 clientCert) : base(clientCert)
        {
        }
    }
}
