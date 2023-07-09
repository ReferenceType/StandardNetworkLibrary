﻿using NetworkLibrary.Generic;
using Protobuff.Components.Serialiser;
using System.Security.Cryptography.X509Certificates;

namespace Protobuff.Pure
{
    public class PureSecureProtoClient : GenericSecureClient<ProtoSerializer>
    {
        public PureSecureProtoClient(X509Certificate2 certificate, bool writeLenghtPrefix = true) : base(certificate, writeLenghtPrefix)
        {
        }
    }
}
