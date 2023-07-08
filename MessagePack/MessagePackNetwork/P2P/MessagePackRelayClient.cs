using MessagePack;
using MessagePackNetwork.Components;
using NetworkLibrary.P2P.Generic;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace MessagePackNetwork.P2P
{
    internal class MessagePackRelayClient : RelayClientBase<MessagepackSerializer>
    {
        public MessagePackRelayClient(X509Certificate2 clientCert) : base(clientCert)
        {
        }
    }
}
