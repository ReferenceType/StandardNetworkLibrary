using MessagePackNetwork.Components;
using NetworkLibrary.TCP.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace MessagePackNetwork.Pure
{
    internal class SecureMessagePackClient : GenericSecureClient<MessagepackSerializer>
    {
        public SecureMessagePackClient(X509Certificate2 certificate, bool writeLenghtPrefix = true) : base(certificate, writeLenghtPrefix)
        {
        }
    }
}
