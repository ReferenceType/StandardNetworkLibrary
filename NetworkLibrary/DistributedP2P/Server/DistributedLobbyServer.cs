using NetworkLibrary.Components.Crypto.Certificate;
using NetworkLibrary.DistributedP2P.Components;
using NetworkLibrary.DistributedP2P.Server.StateManagement;
using NetworkLibrary.DistributedP2P.SimpleRelay;
using NetworkLibrary.MessageProtocol;
using NetworkLibrary.P2P;
using NetworkLibrary.P2P.Components;
using NetworkLibrary.P2P.Components.HolePunch;
using NetworkLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

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
    public class DistributedLobbyServerBase: IDistributedConnection, IDisposable
    {
        public readonly int SSlPort;
        public readonly int TcpPort;
        public readonly int UdpPort;
        public readonly int DiscoveryServerPort;

        private X509Certificate2 serverCertificate;

        SecureMessageServer<MockSerializer> sslServer;


        IAuthenticator authenticator;
        IServerDbConnector dbConnector;

        SessionManager sessionManager;
        Components.StateManager stateManager = new Components.StateManager();
        RelayService piper;
        Stopwatch serverClock = new Stopwatch();

        RelayService relayService;
        RoomManager roomManager = new RoomManager();
        NTPServer ntpServer;
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

            sslServer = new SecureMessageServer<MockSerializer>(SSlPort, serverCertificate);

            ntpServer = new NTPServer(SSlPort, serverClock);
            ntpServer.Start();

            relayService = new RelayService(TcpPort, UdpPort);

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
            var state = new ServerConnectionState(stateId, msg.From, this, authenticator, dbConnector, DiscoveryServerPort);
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
                if (statusList != null)
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

                    var pipeState = new ServerPipeState(message.MessageId, this, relayService);
                    stateManager.RegisterState(pipeState);
                    pipeState.HandleMessage(message);

                    break;


                case InternalConstants.PipeRequestUdp:

                    var pipeState1 = new ServerPipeState(message.MessageId, this, relayService);
                    stateManager.RegisterState(pipeState1);
                    pipeState1.HandleMessage(message);

                    break;

                case InternalConstants.RequestHolepunchUdp:
                    var state = new ServerUdpHolepunchState(message.MessageId, this, sessionManager);
                    stateManager.RegisterState(state);
                    state.HandleMessage(message);
                    break;

                case InternalConstants.RequestSimultaneousHolepunchTcp:
                    var state2 = new ServerSimultaneousTcpHolepunchState(message.MessageId, this, sessionManager);
                    stateManager.RegisterState(state2);
                    state2.HandleMessage(message);
                    break;

                case InternalConstants.RequestSequentialHolepunchTcp:
                    var state3 = new ServerSequentialTcpHolepunchState(message.MessageId, this, sessionManager);
                    stateManager.RegisterState(state3);
                    state3.HandleMessage(message);
                    break;

                case Constants.TimeSync:
                    HandleTimeSync(clientId, message);

                    break;
            }
        }

        private  void HandleTimeSync(Guid clientId, MessageEnvelope message)
        {
            ////if (ctr++ % 2 == 0)
            //{
            //    await Task.Delay(r.Next(50, 300));
            //}
            byte[] time = new byte[8];
            message.Payload = time;
            PrimitiveEncoder.WriteFixedDouble(time, 0, serverClock.Elapsed.TotalMilliseconds);
            message.TimeStamp = DateTime.UtcNow;
            ////if (ctr % 3 == 0)
            //{
            //    await Task.Delay(r.Next(50, 300));
            //}
            SendAsyncMessage(clientId, message);
        }

        int ctr = 0;
        Random r = new Random(42);
        private bool CreateRoom(string roomName, string roomPassword, RoomProtocol protocol)
        {
            if (roomManager.TryCreateRoom(roomName, roomPassword, out Guid RoomId))
            {
                if (relayService.CreateRoom(RoomId, protocol))
                {
                    return true;
                }
            }
            return false;
        }

        private bool GetRoomToken(Guid peerId, Guid roomId, out byte[] token)
        {
            token = relayService.GetRoomToken(peerId, roomId);
            return token != null;
        }

        private bool RemoveFromRoom(Guid peerId, Guid roomId)
        {
            return true;
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

        public Task<MessageEnvelope> SendMessageAndWaitResponse(MessageEnvelope envelope)
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
            relayService.Dispose();
            discoveryServer.Dispose();
            ntpServer.Dispose();
        }


    }
}
