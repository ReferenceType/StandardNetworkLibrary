using NetworkLibrary.TCP.Base;
using NetworkLibrary.TCP.ByteMessage;
using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using NetworkLibrary.Utils;

namespace NetworkLibrary.TCP.SSL.Custom
{
    [Obsolete("Do not use this class, only for test")]
    // authenticate with tls, send with aes.
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
            session.UseQueue = true;
            return session;
        }

        protected override void HandleConnected(SocketAsyncEventArgs e)
        {
#if NET6_0_OR_GREATER

            //var sock = e.ConnectSocket;
            //using (ECDiffieHellmanCng alice = new ECDiffieHellmanCng())
            //{

            //    alice.KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hash;
            //    alice.HashAlgorithm = CngAlgorithm.Sha256;
            //    var alicePublicKey = alice.PublicKey.ToByteArray();

            //    sock.Send(alicePublicKey);

            //    byte[] b =  new byte[140];
            //    int amount =  sock.Receive(b);

            //    var bobPk = b;
            //    CngKey bobKey = CngKey.Import(bobPk, CngKeyBlobFormat.EccPublicBlob);
            //    var privateKey = alice.DeriveKeyMaterial(bobKey);

            //    e.UserToken = ByteCopy.ToArray(privateKey,0,16);
            //}
            //base.HandleConnected(e);
            //return;
#endif

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
