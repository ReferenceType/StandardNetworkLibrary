using NetworkLibrary.TCP.Base.Interface;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace NetworkLibrary.TCP.SSL.Base
{
    public class SslClient
    {
        public Socket clientSocket;
        private X509Certificate2 certificate;
        protected SslStream sslStream;
        public Action< byte[], int, int> OnBytesReceived;
        protected IAsyncSession clientSession;
        public int MaxIndexedMemory = 12800000;
        public SslClient(X509Certificate2 certificate)
        {
            clientSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            this.certificate = certificate;/* new X509Certificate2(certificatePath, cerificatePass);*/
        }

        public virtual void Connect(string ip, int port,string domainName = "example.com")
        {
            clientSocket.Connect(new IPEndPoint(IPAddress.Parse(ip), port));

            sslStream = new SslStream(new NetworkStream(clientSocket, true), false, ValidateCeriticate);
            sslStream.AuthenticateAsClient(domainName,
                new X509CertificateCollection(new[] { certificate }), System.Security.Authentication.SslProtocols.Tls12, true);

            clientSession = CreateSession(Guid.NewGuid(), sslStream);
            clientSession.OnBytesRecieved += HandleBytesReceived;
            clientSession.StartSession();
        }

        protected virtual bool ValidateCeriticate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None || sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors)
                return true;
            return false;
        }

        protected virtual IAsyncSession CreateSession(Guid guid, SslStream sslStream)
        {
            var ses = new SslSession(guid, sslStream);
            ses.MaxIndexedMemory = MaxIndexedMemory;
            return ses;
        }

        protected virtual void HandleBytesReceived(Guid sesonId, byte[] bytes, int offset, int count)
        {
            OnBytesReceived?.Invoke(bytes, offset, count);
        }

        public void SendAsync(byte[] bytes)
        {
            clientSession.SendAsync(bytes);
        }


    }
}
