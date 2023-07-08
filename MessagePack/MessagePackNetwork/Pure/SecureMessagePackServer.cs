using MessagePackNetwork.Components;
using NetworkLibrary.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace MessagePackNetwork.Pure
{
    internal class SecureMessagePackServer : GenericSecureServer<MessagepackSerializer>
    {
        public SecureMessagePackServer(int port, X509Certificate2 certificate, bool writeLenghtPrefix = true) : base(port, certificate, writeLenghtPrefix)
        {
        }
    }
}
