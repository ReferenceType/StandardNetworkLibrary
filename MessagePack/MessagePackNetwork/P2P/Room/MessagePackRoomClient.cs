using MessagePackNetwork.Components;
using NetworkLibrary.P2P.Generic.Room;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace MessagePackNetwork.P2P.Lobby
{
    internal class MessagePackRoomClient : SecureLobbyClient<MessagepackSerializer>
    {
        public MessagePackRoomClient(X509Certificate2 clientCert) : base(clientCert)
        {
        }
    }
}
