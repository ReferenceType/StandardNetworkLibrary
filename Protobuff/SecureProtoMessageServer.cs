using NetworkLibrary;
using NetworkLibrary.Components;
using NetworkLibrary.Components.Statistics;
using NetworkLibrary.MessageProtocol.Fast;
using Protobuff.Components;
using Protobuff.Components.Serialiser;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Protobuff
{
    public class SecureProtoMessageServer : GenericSecureMessageServerWrapper<ProtoSerializer>
    {
        public SecureProtoMessageServer(int port, X509Certificate2 cerificate) : base(port, cerificate)
        {
        }
    }


}
