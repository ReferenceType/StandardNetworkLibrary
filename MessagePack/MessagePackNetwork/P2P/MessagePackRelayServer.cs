using MessagePackNetwork.Components;
using NetworkLibrary.P2P.Generic;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace MessagePackNetwork.P2P
{
    internal class MessagePackRelayServer : SecureRelayServerBase<MessagepackSerializer>
    {
        public MessagePackRelayServer(int port, X509Certificate2 cerificate) : base(port, cerificate)
        {
        }
    }
}
