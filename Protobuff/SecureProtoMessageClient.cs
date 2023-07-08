using NetworkLibrary;
using NetworkLibrary.Components;
using NetworkLibrary.Components.Statistics;
using NetworkLibrary.MessageProtocol.Fast;
using Protobuff.Components;
using Protobuff.Components.Serialiser;
using System;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Protobuff
{
    public class SecureProtoMessageClient : GenericSecureMessageClientWrapper<ProtoSerializer>
    {
        public SecureProtoMessageClient(X509Certificate2 certificate) : base(certificate)
        {
        }
    }
}
