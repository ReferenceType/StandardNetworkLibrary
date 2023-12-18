using NetworkLibrary.TCP.Base;
using NetworkLibrary.TCP.ByteMessage;
using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace NetworkLibrary.TCP.SSL.Custom
{
    [Obsolete("Do not use this class, only for test")]
    public class CustomSslServer : ByteMessageTcpServer
    {

        private X509Certificate2 certificate;
        public CustomSslServer(int port, X509Certificate2 certificate) : base(port)
        {
            this.certificate = certificate;
            
        }

        protected override bool IsConnectionAllowed(SocketAsyncEventArgs acceptArgs)
        {
#if NET6_0_OR_GREATER

            //var sock = acceptArgs.AcceptSocket;
            //using (ECDiffieHellmanCng alice = new ECDiffieHellmanCng())
            //{

            //    alice.KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hash;
            //    alice.HashAlgorithm = CngAlgorithm.Sha256;
            //    var alicePublicKey = alice.PublicKey.ToByteArray();

            //    sock.SendAsync(alicePublicKey, SocketFlags.None);

            //    byte[] b = new byte[140];
            //    int amount = sock.Receive(b);

              
            //    var bobPk = b;
            //    CngKey bobKey = CngKey.Import(bobPk, CngKeyBlobFormat.EccPublicBlob);
            //    var privateKey = alice.DeriveKeyMaterial(bobKey);

            //    acceptArgs.UserToken = Utils.ByteCopy.ToArray(privateKey, 0, 16);

            //}
            //return true;
#endif

            // Todo do ssl part of server here
            var sslStream = new SslStream(new NetworkStream(acceptArgs.AcceptSocket, false), false, ValidateCeriticate);
            sslStream.AuthenticateAsServer(certificate, true, System.Security.Authentication.SslProtocols.Tls12, false);
            // create key for this client

            var rnd =  RandomNumberGenerator.Create();
            var byteKey = new byte[16];

            rnd.GetNonZeroBytes(byteKey);
            rnd.Dispose();
            acceptArgs.UserToken = byteKey;

            sslStream.Write(byteKey, 0, byteKey.Length);

            sslStream.Read(new byte[1], 0, 1);
            sslStream.Close();
            sslStream.Dispose();
            return true;
        }


        private bool ValidateCeriticate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None || sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors)
                return true;
            return false;
        }


        // override create session
        private protected override IAsyncSession CreateSession(SocketAsyncEventArgs e, Guid sessionId)
        {

            var session = new CustomSslSession(e, sessionId, (byte[])e.UserToken);
            session.socketSendBufferSize = ClientSendBufsize;
            session.SocketRecieveBufferSize = ClientReceiveBufsize;
            session.MaxIndexedMemory = MaxIndexedMemoryPerClient;
            session.DropOnCongestion = DropOnBackPressure;
            session.UseQueue = true;
            return session;
        }
    }

}
