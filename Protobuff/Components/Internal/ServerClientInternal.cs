using MessageProtocol;
using NetworkLibrary.MessageProtocol;
using Protobuff.Components.Serialiser;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Protobuff.Components.Internal
{
    internal class ProtoClientInternal : MessageClient<ProtoSerializer> {}

    internal class ProtoServerInternal : MessageServer<ProtoSerializer>
    {
        internal ProtoServerInternal(int port) : base(port)
        {
        }
    }


    public class SecureProtoClientInternal : SecureMessageClient<ProtoSerializer>
    {
        public SecureProtoClientInternal(X509Certificate2 certificate) : base(certificate)
        {
        }
    }

    internal class SecureProtoServerInternal : SecureMessageServer<ProtoSerializer>
    {
        public SecureProtoServerInternal(int port, X509Certificate2 certificate) : base(port, certificate)
        {

        }
    }
}
