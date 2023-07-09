using MessagePackNetwork.Components;
using NetworkLibrary.P2P.Generic.Room;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace MessagePackNetwork.P2P.Room
{
    internal class MessagePackRoomServer : SecureLobbyServer<MessagepackSerializer>
    {
        public MessagePackRoomServer(int port, X509Certificate2 cerificate) : base(port, cerificate)
        {
        }
    }
}
