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
    internal class SecureMessagePackMessageClient : GenericSecureMessageClientWrapper<MessagepackSerializer>
    {
        public SecureMessagePackMessageClient(X509Certificate2 certificate) : base(certificate)
        {
        }
    }
}
