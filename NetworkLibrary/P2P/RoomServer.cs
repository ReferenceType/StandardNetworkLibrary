using NetworkLibrary.P2P.Components;
using NetworkLibrary.P2P.Generic.Room;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace NetworkLibrary.P2P
{
    public class RoomServer : SecureLobbyServer<MockSerializer>
    {
        public RoomServer(int port, X509Certificate2 cerificate) : base(port, cerificate)
        {
        }
    }
}
