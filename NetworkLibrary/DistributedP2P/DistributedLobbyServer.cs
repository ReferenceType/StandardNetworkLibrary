using NetworkLibrary.Components.Crypto.Certificate;
using NetworkLibrary.DistributedP2P.Components;
using NetworkLibrary.P2P.Components.StateManagement;
using NetworkLibrary.TCP.Base;
using NetworkLibrary.TCP.SSL.Base;
using NetworkLibrary.UDP;
using NetworkLibrary.MessageProtocol;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace NetworkLibrary.DistributedP2P
{

    public class Dependencies
    {
        public IAuthenticator Authenticator;
        public IClientDbConnector DbConnector;
    }
    public class ServerParameters
    {
        public X509Certificate2 certificate;
        public int SSlPort;
        public int TcpPort;
        public int UdpPort;
    }
    public class DistributedLobbyServerBase<S> where S : ISerializer, new() 
    {
        public readonly int SSlPort;
        public readonly int TcpPort;
        public readonly int UdpPort;

        private X509Certificate2 serverCertificate;

        SecureMessageServer<S> sslServer;
        AsyncTcpServer tcpServer;
        AsyncUdpServer udpServer;

        IAuthenticator authenticator;
        IClientDbConnector dbConnector;

        SessionManager<S> sessionManager;
        public DistributedLobbyServerBase(Dependencies dependencies, ServerParameters parameters)
        {
            this.authenticator = dependencies.Authenticator;
            this.dbConnector = dependencies.DbConnector;
            SSlPort = parameters.SSlPort;
            TcpPort = parameters.TcpPort;
            UdpPort = parameters.UdpPort;

            serverCertificate = parameters.certificate ?? CertificateGenerator.GenerateSelfSignedCertificate();
        }


        public void StartServer()
        {
            sslServer = new SecureMessageServer<S>(SSlPort, serverCertificate);
            tcpServer = new AsyncTcpServer(TcpPort);
            udpServer = new AsyncUdpServer(UdpPort);

            sslServer.OnClientRequestedConnection += ValidateSslConnection;
            sslServer.OnClientAccepted += SslClientAccepted;
            sslServer.OnClientDisconnected += SslClientDisconnected;

            tcpServer.OnClientAccepting += ValidateTcpConnection;
            tcpServer.OnClientAccepted += TcpClientAccepted;
            tcpServer.OnClientDisconnected += TcpclientDisconnected;

            sslServer.OnMessageReceived += SslMessageReceived;
            tcpServer.OnBytesReceived += TcpBytesReceived;
            udpServer.OnBytesRecieved += UdpBytesReceived;

            udpServer.StartServer();
            tcpServer.StartServer();
            sslServer.StartServer();
        }

       

        private bool ValidateTcpConnection(Socket acceptedSocket)
        {
            return true;
        }

        private void TcpClientAccepted(Guid guid)
        {
            sessionManager.TcpPipeCreated(guid);
        }
        private void TcpclientDisconnected(Guid guid)
        {
            sessionManager.HandleTcpPipeDisconnect(guid);
        }

        private void SslClientDisconnected(Guid guid)
        {
            sessionManager.HandleDisconnect(guid);
        }

        private bool ValidateSslConnection(Socket acceptedSocket)
        {
            // we will do Ddos protection here;
            return true;
        }

       
        private void SslClientAccepted(Guid guid)
        {
            // this must be async, later.
            MessageConnection<S> messageConnection = new MessageConnection<S>(sslServer);
            IAuthenticationResult result = authenticator.Authenticate(messageConnection, guid);

            var sessionEp = sslServer.GetSessionEndpoint(guid);
            sessionManager.CreateSession(messageConnection, result, guid, sessionEp);
        }

        #region Receive
        // so here message can be p2p message, clients business.
        // internal messages
        private void SslMessageReceived(Guid guid, MessageEnvelope envelope)
        {
            if (envelope.IsInternal) 
            {
                HandleInternalMessage(guid, envelope);
            }
            else
            {
                sessionManager.HandleMessage(guid, envelope);
            }
        }

        private void HandleInternalMessage(Guid guid, MessageEnvelope envelope)
        {
            //server messages.
        }

        private void TcpBytesReceived(Guid guid, byte[] bytes, int offset, int count)
        {
            sessionManager.RouteP2PMessageTcp(guid, bytes, offset, count);
        }

        private void UdpBytesReceived(IPEndPoint adress, byte[] bytes, int offset, int count)
        {
            sessionManager.RouteP2PMessageUdp(adress, bytes, offset, count);
        }
        #endregion

        public void ShutDownServer() 
        {
            sslServer.ShutdownServer();
            tcpServer.ShutdownServer();
            udpServer.Dispose();
        }
    }
}
