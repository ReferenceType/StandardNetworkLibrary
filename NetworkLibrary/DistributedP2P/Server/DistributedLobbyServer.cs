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
using System.Security.Cryptography;
using NetworkLibrary.DistributedP2P.SimpleRelay;
using NetworkLibrary.Utils;
using NetworkLibrary.P2P.Components.HolePunch;

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
        public int DiscoveryServerPort;
    }
    public class DistributedLobbyServerBase<S> : IDistributedConnection,IDisposable where S : ISerializer, new()
    {
        public readonly int SSlPort;
        public readonly int TcpPort;
        public readonly int UdpPort;
        public readonly int DiscoveryServerPort;

        private X509Certificate2 serverCertificate;

        SecureMessageServer<S> sslServer;


        IAuthenticator authenticator;
        IServerDbConnector dbConnector;

        SessionManager sessionManager;
        Components.StateManager stateManager =  new Components.StateManager();
        RelayService piper;
        Stopwatch serverClock = new Stopwatch();

        RelayService pipeManager;

        private byte[] serverKey = new byte[16];

        EndpointDiscoveryServer discoveryServer;
        public DistributedLobbyServerBase(Dependencies dependencies, ServerParameters parameters)
        {
            authenticator = dependencies.Authenticator;
            dbConnector = dependencies.DbConnector;
            SSlPort = parameters.SSlPort;
            TcpPort = parameters.TcpPort;
            UdpPort = parameters.UdpPort;
            DiscoveryServerPort = parameters.DiscoveryServerPort;

            serverCertificate = parameters.certificate ?? CertificateGenerator.GenerateSelfSignedCertificate();
        }


        public void StartServer()
        {
            serverClock.Start();

            sslServer = new SecureMessageServer<S>(SSlPort, serverCertificate);

            var random = RandomNumberGenerator.Create();
            var key = new byte[32];
            random.GetNonZeroBytes(key);
            pipeManager = new RelayService(TcpPort, UdpPort, key);

            sslServer.OnClientRequestedConnection += ValidateSslConnection;
            sslServer.OnClientAccepted += SslClientAccepted;
            sslServer.OnClientDisconnected += SslClientDisconnected;

            sslServer.OnMessageReceived += SslMessageReceived;
            sslServer.StartServer();

            sessionManager = new SessionManager(this);
            sessionManager.PeerListPublish += PublishPeerList;

            discoveryServer = new EndpointDiscoveryServer(DiscoveryServerPort);
            discoveryServer.Start();
        }

      

        private bool ValidateSslConnection(Socket acceptedSocket)
        {
            // we will do Ddos protection here;
            return true;
        }


        private void SslClientAccepted(Guid ephemeralClientId)
        {
            TimerService.RegisterTimer(ephemeralClientId, 20000, () => 
            { 
                if (!sessionManager.IsSessionActive(ephemeralClientId)) 
                {
                    sslServer.CloseSession(ephemeralClientId);
                }
            });
        }

        private void HandleConnRequest(MessageEnvelope msg)
        {

            Guid stateId = msg.MessageId;
            var state = new ServerConnectionState(stateId, msg.From, this, authenticator, dbConnector,DiscoveryServerPort);
            stateManager.RegisterState(state);
            state.HandleMessage(msg);

            state.OnComplete += ConnectionStateComplete;
        }

        private void ConnectionStateComplete(IConversationState state_)
        {
            var state = (ServerConnectionState)state_;
            if (state.IsSuccesful)
            {
                var sessionEp = sslServer.GetSessionEndpoint(state.EphemeralClientId);
                var statusList = sessionManager.CreateSession(state.clientDbInfo, state.EphemeralClientId, sessionEp, state.clientLocalIps);
                if(statusList!=null)
                    PublishPeerList(new List<PeerStatusList>() { statusList });
            }
        }

        private void PublishPeerList(List<PeerStatusList> pubList)
        {
            var msg = new MessageEnvelope();
            msg.Header = InternalConstants.PublishPeerList;
            msg.IsInternal = true;

            var stream = SharerdMemoryStreamPool.RentStreamStatic();
            foreach (var pubData in pubList)
            {
                stream.Position32 = 0;
                KnownTypeSerializer.SerializePeerStatusList(stream, pubData);
                msg.SetPayload(stream.GetBuffer(), 0, stream.Position32);

                sslServer.SendAsyncMessage(pubData.WhoNeedsToKnow, msg);
            }
            SharerdMemoryStreamPool.ReturnStreamStatic(stream);
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

            message.From = clientId;
           
            if (stateManager.HandleMessage(message))
                return;

           

            switch (message.Header)
            {
                case InternalConstants.ConnectionReq:
                    HandleConnRequest(message);
                    break;

                case InternalConstants.PipeRequestTcp:

                    var pipeState = new ServerPipeState(message.MessageId, this, pipeManager);
                    stateManager.RegisterState(pipeState);
                    pipeState.HandleMessage(message);

                break;


                case InternalConstants.PipeRequestUdp:

                    var pipeState1 = new ServerPipeState(message.MessageId, this, pipeManager);
                    stateManager.RegisterState(pipeState1);
                    pipeState1.HandleMessage(message);

                    break;

                case InternalConstants.RequestHolepunchUdp:
                    var state = new ServerUdpHolepunchState(message.MessageId, this, sessionManager);
                    stateManager.RegisterState(state);
                    state.HandleMessage(message);
                    break;

                case InternalConstants.RequestSequentialHolepunchTcp:
                    var state2 = new ServerTcpHolepunchState(message.MessageId, this, sessionManager);
                    stateManager.RegisterState(state2);
                    state2.HandleMessage(message);
                    break;

                case InternalConstants.RequestSimultaneousHolepunchTcp:
                    var state3 = new ServerTcpHolepunchState2(message.MessageId, this, sessionManager);
                    stateManager.RegisterState(state3);
                    state3.HandleMessage(message);
                    break;

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
        public void SendAsyncMessage(MessageEnvelope message)
        {
            sslServer.SendAsyncMessage(message.To, message);
        }
        public Task<MessageEnvelope> SendMessageAndWaitResponse(Guid destination, MessageEnvelope envelope)
        {
            return sslServer.SendMessageAndWaitResponse(destination, envelope);
        }

        public Task<MessageEnvelope> SendMessageAndWaitResponse( MessageEnvelope envelope)
        {
            return sslServer.SendMessageAndWaitResponse(envelope.To, envelope);
        }

        private void SslClientDisconnected(Guid guid)
        {
            sessionManager.DestroySession(guid);
        }

        public void ShutDownServer()
        {
            sslServer.ShutdownServer();

        }

        public DateTime GetDateTime()
        {
            // will be distributed time
            return DateTime.UtcNow;
        }

        public double GetTime() 
        {
            return serverClock.Elapsed.TotalMilliseconds;
        }
        public Task<bool> SyncTime()
        {
            throw new NotImplementedException();
        }
        public void Dispose()
        {
            sslServer.ShutdownServer();
            pipeManager.Dispose();
            discoveryServer.Dispose();
        }

       
    }
}
