using NetworkLibrary.MessageProtocol;
using NetworkLibrary.P2P.Components;
using NetworkLibrary.P2P.Generic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace NetworkLibrary.P2P
{
    public class RelayServer : SecureRelayServerBase<MockSerializer>
    {
        public RelayServer(int port, X509Certificate2 cerificate) : base(port, cerificate)
        {
        }
    }
    
    
}
