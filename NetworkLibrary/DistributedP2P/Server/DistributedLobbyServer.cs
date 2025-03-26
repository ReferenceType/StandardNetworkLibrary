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
using System.Data.Common;
using NetworkLibrary.P2P;
using System.Diagnostics;

using System.Threading.Tasks;
using NetworkLibrary.DistributedP2P.Server.StateManagement;

namespace NetworkLibrary.DistributedP2P.Server
{

    public class Dependencies
    {
        public IAuthenticator Authenticator;
        public IServerDbConnector DbConnector;
    }
    public class ServerParameters
    {
        public X509Certificate2 certificate;
        public int SSlPort;
        public int TcpPort;
        public int UdpPort;
    }
    public class DistributedLobbyServerBase<S> : IDistributedConnection where S : ISerializer, new()
    {
        public readonly int SSlPort;
        public readonly int TcpPort;
        public readonly int UdpPort;

        private X509Certificate2 serverCertificate;

        SecureMessageServer<S> sslServer;
        AsyncTcpServer tcpServer;
        AsyncUdpServer udpServer;

        IAuthenticator authenticator;
        IServerDbConnector dbConnector;

        SessionManager<S> sessionManager;
        Components.StateManager stateManager =  new Components.StateManager();
        PipeAssociator piper;
        Stopwatch serverClock = new Stopwatch();

        private byte[] serverKey = new byte[16];
        public DistributedLobbyServerBase(Dependencies dependencies, ServerParameters parameters)
        {
            authenticator = dependencies.Authenticator;
            dbConnector = dependencies.DbConnector;
            SSlPort = parameters.SSlPort;
            TcpPort = parameters.TcpPort;
            UdpPort = parameters.UdpPort;

            serverCertificate = parameters.certificate ?? CertificateGenerator.GenerateSelfSignedCertificate();
        }


        public void StartServer()
        {
            serverClock.Start();

            sslServer = new SecureMessageServer<S>(SSlPort, serverCertificate);
            tcpServer = new AsyncTcpServer(TcpPort);
            udpServer = new AsyncUdpServer(UdpPort);

            sslServer.OnClientRequestedConnection += ValidateSslConnection;
            sslServer.OnClientAccepted += SslClientAccepted;
            sslServer.OnClientDisconnected += SslClientDisconnected;

            tcpServer.OnClientAccepting += ValidateTcpConnection;
            sslServer.OnMessageReceived += SslMessageReceived;

            udpServer.StartServer();
            tcpServer.StartServer();
            sslServer.StartServer();

            sessionManager = new SessionManager<S>(this, tcpServer, udpServer, serverKey);

        }

        private bool ValidateSslConnection(Socket acceptedSocket)
        {
            // we will do Ddos protection here;
            return true;
        }

        private bool ValidateTcpConnection(Socket acceptedSocket)
        {
            return true;
        }

        private void SslClientAccepted(Guid ephemeralClientId)
        {
            Guid stateId = Guid.NewGuid();
            var state = new ServerConnectionState(stateId, ephemeralClientId, this, authenticator, dbConnector);
            stateManager.RegisterState(state);

            state.Start();

            state.OnComplete += ConnectionStateComplete;
        }

        private void ConnectionStateComplete(IConversationState state_)
        {
            var state = (ServerConnectionState)state_;
            if (state.IsSuccesful)
            {
                var sessionEp = sslServer.GetSessionEndpoint(state.EphemeralClientId);
                sessionManager.CreateSession(state.clientDbInfo, state.EphemeralClientId, sessionEp);
            }
        }

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

        private void HandleInternalMessage(Guid clientId, MessageEnvelope message)
        {
            //server messages.
            // time sync
            // holepunching
            // ping

            if (sessionManager.HandleMessage(clientId, message))
                return;

            switch (message.Header)
            {
                case Constants.TimeSync:

                    byte[] time = new byte[8];
                    message.Payload = time;
                    PrimitiveEncoder.WriteFixedDouble(time, 0, serverClock.Elapsed.TotalMilliseconds);
                    message.TimeStamp = DateTime.UtcNow;
                    SendAsyncMessage(clientId, message);

                    break;
            }
        }

        public void SendAsyncMessage(Guid clientId, MessageEnvelope message)
        {
            sslServer.SendAsyncMessage(clientId, message);
        }

        public Task<MessageEnvelope> SendMessageAndWaitResponse(Guid destination, MessageEnvelope envelope)
        {
            return sslServer.SendMessageAndWaitResponse(destination, envelope);
        }

        private void SslClientDisconnected(Guid guid)
        {
            sessionManager.DestroySession(guid);
        }

        public void ShutDownServer()
        {
            sslServer.ShutdownServer();
            tcpServer.ShutdownServer();
            udpServer.Dispose();
        }

        public DateTime GetTime()
        {
            // will be distributed time
            return DateTime.UtcNow;
        }
    }
}
