using NetworkLibrary.TCP.Base;
using NetworkLibrary.TCP.SSL.Base;
using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace NetworkLibrary.TCP.SSL.ByteMessage
{
    public class SslByteMessageServer : SslServer
    {
        public SslByteMessageServer(int port, int maxClients, X509Certificate2 certificate) : base(port, maxClients, certificate)
        {
        }

        internal override IAsyncSession CreateSession(Guid guid, SslStream sslStream, BufferProvider bufferProvider)
        {
            var ses =  new SslByteMessageSession(guid, sslStream, bufferProvider);
            ses.MaxIndexedMemory = MaxIndexedMemoryPerClient;

            if (GatherConfig == ScatterGatherConfig.UseQueue)
                ses.UseQueue = true;
            else
                ses.UseQueue = false;

            return ses;
        }
    }
}
