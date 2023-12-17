using NetworkLibrary.TCP.Base;
using NetworkLibrary.TCP.ByteMessage;
using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace NetworkLibrary.TCP.SSL.Custom
{
    public class CustomSslClient : ByteMessageTcpClient
    {
        private X509Certificate2 certificate;
        public CustomSslClient(X509Certificate2 certificate)
        {
            this.certificate = certificate;
        }

        private protected override IAsyncSession CreateSession(SocketAsyncEventArgs e, Guid sessionId)
        {
            var session = new CustomSslSession(e, sessionId, (byte[])e.UserToken);
            session.socketSendBufferSize = SocketSendBufferSize;
            session.SocketRecieveBufferSize = SocketRecieveBufferSize;
            session.MaxIndexedMemory = MaxIndexedMemory;
            session.DropOnCongestion = DropOnCongestion;
            session.UseQueue = false;
            return session;
        }



        // this was just for a test.
        protected override void HandleConnected(SocketAsyncEventArgs e)
        {
            // do the ssl validation certs,
            // get your aes symetric key,
            var sslStream = new SslStream(new NetworkStream(e.ConnectSocket, false), false, ValidateCeriticate);
            sslStream.AuthenticateAsClient("example.com",
                new X509CertificateCollection(new[] { certificate }), System.Security.Authentication.SslProtocols.Tls12, true);

            byte[] aesKey = new byte[16];
            sslStream.Read(aesKey, 0, 16);
            e.UserToken = aesKey;
            // ack
            sslStream.Write(new byte[1]);
            sslStream.Flush();

            sslStream.Close();
            sslStream.Dispose();

            base.HandleConnected(e);
        }

        protected virtual bool ValidateCeriticate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None || sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors)
                return true;
            return false;
        }


    }
}
