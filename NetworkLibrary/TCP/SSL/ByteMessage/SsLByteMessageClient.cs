using NetworkLibrary.TCP.Base.Interface;
using NetworkLibrary.TCP.SSL.Base;
using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace NetworkLibrary.TCP.SSL.ByteMessage
{
    public class SsLByteMessageClient : SslClient
    {
        public SsLByteMessageClient(X509Certificate2 certificate) : base(certificate)
        {
        }


        protected override IAsyncSession CreateSession(Guid guid, SslStream sslStream)
        {
            var ses =  new SSLByteMessageSession(guid, sslStream);
            ses.MaxIndexedMemory = MaxIndexedMemory;
            return ses;
        }
    }

}
