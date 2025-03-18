using MessagePack;
using MessagePackNetwork.Components;
using NetworkLibrary.MessageProtocol.Fast;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace MessagePackNetwork.MessageProtocol
{
    public class SecureMessagePackMessageServer : GenericSecureMessageServerWrapper<MessagepackSerializer>
    {
        public SecureMessagePackMessageServer(int port, X509Certificate2 cerificate) : base(port, cerificate)
        {
        }
    }
}
