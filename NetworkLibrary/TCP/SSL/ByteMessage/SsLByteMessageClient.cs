using NetworkLibrary.TCP.Base;
using NetworkLibrary.TCP.SSL.Base;
using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace NetworkLibrary.TCP.SSL.ByteMessage
{
    public class SslByteMessageClient : SslClient
    {
        public SslByteMessageClient(X509Certificate2 certificate) : base(certificate)
        {
        }


        internal override IAsyncSession CreateSession(Guid guid, SslStream sslStream, BufferProvider bufferProvider)
        {
            var ses =  new SslByteMessageSession(guid, sslStream, bufferProvider);
            ses.MaxIndexedMemory = MaxIndexedMemory;
            ses.OnSessionClosed += (id) => OnDisconnected?.Invoke();
            return ses;
        }
    }

}
