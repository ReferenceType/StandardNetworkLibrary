using NetworkLibrary.Components.Crypto;
using NetworkLibrary.Components.Crypto.DiffieHellman;
using NetworkLibrary.Components.Crypto.KeyDerivation;
using NetworkLibrary.DistributedP2P.Client.StateManagement;
using NetworkLibrary.DistributedP2P.Components;
using NetworkLibrary.DistributedP2P.Server;
using NetworkLibrary.MessageProtocol;
using NetworkLibrary.P2P.Components.HolePunch;
using NetworkLibrary.TCP.AES;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NetworkLibrary.Components;
using System.Net;
using NetworkLibrary.DistributedP2P.Channels.Components;

namespace NetworkLibrary.DistributedP2P.Client
{
    public class DistributedLobbyClient<S> : IDistributedConnection, IDisposable where S : ISerializer, new()
    {
        IClientDbConnection clientDbConnector;
        IClientAuthenticationProvider clientAuthProvider;
        SecureMessageClient<S> sslClient;
        StateManager stateManager;
        TimeSync timeSync;

        private ConcurrentDictionary<Guid, PeerStatus> onlinePeers = new ConcurrentDictionary<Guid, PeerStatus>();

        public event Action<IChannel> PeerConnected;
        public event Action<PeerStatus> PeerOnline;
        public event Action<PeerStatus> PeerOffline;

        public event Action<MessageEnvelope> MessageReceived;
        public event Action Disconnected;

        public Guid SessionId { get; private set; }

        private EndpointData DiscoveryServerEndpoint;
        private int connected = 0;
        public bool IsConnected 
        {
            get => Interlocked.CompareExchange(ref connected, 0, 0) == 1;
            private set => Interlocked.Exchange(ref connected, value ? 1 : 0); 
        }

        private EndpointData serverEndpoint= new EndpointData();

        bool isDisposed = false;
        ILogger logger;

        public DistributedLobbyClient(IClientDbConnection clientDbConnector,
                                      IClientAuthenticationProvider clientAuthProvider,
                                      ILogger logger,
                                      X509Certificate2 certificate = null)
        {
            this.clientDbConnector = clientDbConnector;
            this.clientAuthProvider = clientAuthProvider;
            this.logger=logger;

            stateManager = new StateManager(logger);

            sslClient = new SecureMessageClient<S>(certificate);
            sslClient.OnMessageReceived += HandleServerMsg;
            sslClient.OnDisconnected += HandleDisconnected;
            timeSync = new TimeSync(this);
        }

        public async Task<bool> ConnectAsync(string ip, int port)
        {
            if (isDisposed)
                throw new ObjectDisposedException(this.ToString());

            if (IsConnected)
                return true;

            IClientAuthenticationToken authToken = clientAuthProvider.Authenticate();

            bool res = await sslClient.ConnectAsync(ip, port);
            if (res)
            {
                serverEndpoint = new EndpointData(ip, port);
                timeSync.SetEndpoint(serverEndpoint);

                Guid conversationId = Guid.NewGuid();
                var conState = new ClientConnectionState(conversationId, this, clientDbConnector, authToken, logger);
                stateManager.RegisterState(conState);
                conState.Start();

                await conState.WaitCompletion();

                if (conState.IsSuccesful)
                {
                    SessionId = conState.SessionId;
                    DiscoveryServerEndpoint = new EndpointData(ip, conState.EDSPort);
                    Console.WriteLine($"Connected to server {ip}:{port} with session {SessionId} and discovery port {conState.EDSPort}");
                    IsConnected = true;
                    timeSync.StartAutoTimeSync();
                    return true;
                }

                return false;

            }
            else return false;
        }

        #region Send
        public void SendAsyncMessage(MessageEnvelope message)
        {
            sslClient.SendAsyncMessage(message);
        }

        public Task<MessageEnvelope> SendMessageAndWaitResponse(MessageEnvelope message)
        {
            return sslClient.SendMessageAndWaitResponse(message);
        }
        public void SendAsyncMessage(Guid a, MessageEnvelope msg)
        {
            msg.To = a;
            sslClient.SendAsyncMessage(msg);
        }

        public Task<MessageEnvelope> SendMessageAndWaitResponse(Guid a, MessageEnvelope msg)
        {
            msg.To = a;
            return sslClient.SendMessageAndWaitResponse(msg);
        }
        #endregion

        private void HandleServerMsg(MessageEnvelope envelope)
        {

            if (envelope.IsInternal)
            {
                if (stateManager.HandleMessage(envelope))
                    return;

                switch (envelope.Header)
                {
                    case InternalConstants.PipeRequestTcp:
                    case InternalConstants.PipeRequestUdp:

                        var pipeState = new ClientPipeState(envelope, this, serverEndpoint, logger);
                        pipeState.OnComplete += HandlePipeCreated;
                        stateManager.RegisterState(pipeState);
                        pipeState.HandleMessage(envelope);
                        break;


                    case InternalConstants.PublishPeerList:

                        var buffer = envelope.Payload;
                        int offset = envelope.PayloadOffset;

                        PeerStatusList statusList = KnownTypeSerializer.DeserializePeerStatusList(buffer, ref offset);
                        PublishPeerStatusEvents(statusList);

                        break;

                    case InternalConstants.RequestHolepunchUdp:
                        ManageUdpHolepunchRequest(envelope);
                        break;

                    case InternalConstants.RequestSequentialHolepunchTcp:
                        ManageSequentialTcpHolepunchReq(envelope);
                        break;

                    case InternalConstants.RequestSimultaneousHolepunchTcp:
                        ManageSimyltaneousTcpHolepunchReq(envelope);
                      
                        break;

                }
            }
            else
            {
                MessageReceived?.Invoke(envelope);
            }


        }

       

        private void PublishPeerStatusEvents(PeerStatusList statusList)
        {
            foreach (var offlineKV in statusList.WentOffline)
            {
                if (onlinePeers.TryRemove(offlineKV.Value.EphemeralId, out _))
                    PeerOffline?.Invoke(offlineKV.Value);
            }

            foreach (var onlineKV in statusList.NewOnline)
            {
                if (onlinePeers.TryAdd(onlineKV.Value.EphemeralId, onlineKV.Value))
                    PeerOnline?.Invoke(onlineKV.Value);
            }
        }

        public Dictionary<Guid, PeerStatus> GetPeerList()
        {
            Dictionary<Guid, PeerStatus> copy = new Dictionary<Guid, PeerStatus>();
            foreach (var peerKv in onlinePeers)
            {
                var sts = new PeerStatus();
                sts.EphemeralId = peerKv.Value.EphemeralId;
                sts.PeerId = peerKv.Value.PeerId;
                sts.OnlineSince = peerKv.Value.OnlineSince;

                copy[peerKv.Key] = sts;
            }
            return copy;
        }

        public async Task<IChannel> OpenRelayChannel(Guid destinationPeer, ChannelInfo Info)
        {
            var pipeState = new ClientPipeState(Guid.NewGuid(), this, serverEndpoint, Info, logger);
            stateManager.RegisterState(pipeState);
            pipeState.Start(destinationPeer);

            await pipeState.WaitCompletion();

            if (pipeState.IsSuccesful)
            {
                IChannel channel = ChannelFactory.CreateChannel(pipeState, true, logger);
                return channel;
            }
            return null;
        }

        private void HandlePipeCreated(IConversationState state)
        {
            if (state.IsSuccesful)
            {
                var pipeState = (ClientPipeState)state;
                IChannel channel = ChannelFactory.CreateChannel(pipeState, false, logger);

                if (channel != null)
                    PeerConnected?.Invoke(channel);

                Console.WriteLine("DestPeer Conn Succesfull");
                // notify that a connection is opened, like socket accept
            }
        }

        public async Task<IChannel> TryHolePunch(Guid destination, ChannelInfo info, TcpHolePunchStrategy strategy = TcpHolePunchStrategy.Sequential) 
        {
            
            if(info.ChannelType == ChannelType.Udp || info.ChannelType == ChannelType.SecureUdp)
            {
                var state = new ClientUdpHolepunchState(Guid.NewGuid(), destination, this, serverEndpoint, DiscoveryServerEndpoint, info, logger);
                stateManager.RegisterState(state);
                state.Start();

                await state.WaitCompletion();

                if (state.IsSuccesful)
                {
                    return ChannelFactory.CreateChannel(state, true, logger);
                }
                return null;
            }
            else
            {
                if (strategy == TcpHolePunchStrategy.Sequential)
                {
                    var state = new ClientSequentialTcpHolepunchState(Guid.NewGuid(), destination, this, serverEndpoint, DiscoveryServerEndpoint, info, logger);
                    stateManager.RegisterState(state);
                    state.Start();

                    await state.WaitCompletion();

                    if (state.IsSuccesful)
                    {
                        return ChannelFactory.CreateChannel(state, true, logger);
                    }
                    return null;
                }
                else
                {
                    var state = new ClientSimultaneousTcpHolepunchState(Guid.NewGuid(), destination, this, serverEndpoint, DiscoveryServerEndpoint, info, logger);
                    stateManager.RegisterState(state);
                    state.Start();

                    await state.WaitCompletion();

                    if (state.IsSuccesful)
                    {
                        return ChannelFactory.CreateChannel(state, true, logger);
                    }
                    return null;
                }
              
            }
         
        }

        private void ManageUdpHolepunchRequest(MessageEnvelope envelope)
        {
            var state = new ClientUdpHolepunchState(envelope.MessageId, envelope.From, this,serverEndpoint,DiscoveryServerEndpoint, null, logger);
            stateManager.RegisterState(state);
            state.OnComplete += State_OnComplete;
            state.HandleMessage(envelope);

            void State_OnComplete(IConversationState obj)
            {
                if (obj.IsSuccesful)
                {
                    var ch = ChannelFactory.CreateChannel(state, false, logger);
                    PeerConnected?.Invoke(ch);
                }
              
            }

        }

        private void ManageSimyltaneousTcpHolepunchReq(MessageEnvelope envelope)
        {
            var state = new ClientSimultaneousTcpHolepunchState(envelope.MessageId, envelope.From, this, serverEndpoint, DiscoveryServerEndpoint, null, logger);
            stateManager.RegisterState(state);
            state.OnComplete += State_OnComplete;
            state.HandleMessage(envelope);

            void State_OnComplete(IConversationState obj)
            {
                if (obj.IsSuccesful)
                {
                    var ch = ChannelFactory.CreateChannel(state, false, logger);
                    PeerConnected?.Invoke(ch);
                }   
            }
        }

        private void ManageSequentialTcpHolepunchReq(MessageEnvelope envelope)
        {
            var state = new ClientSequentialTcpHolepunchState(envelope.MessageId, envelope.From, this, serverEndpoint, DiscoveryServerEndpoint, null, logger);
            stateManager.RegisterState(state);
            state.OnComplete += State_OnComplete;
            state.HandleMessage(envelope);

            void State_OnComplete(IConversationState obj)
            {
                if (obj.IsSuccesful)
                {
                    var ch = ChannelFactory.CreateChannel(state, false, logger);
                    PeerConnected?.Invoke(ch);
                }
            }
        }

        public double GetTime()
        {
            return timeSync.GetTime();
        }

        public DateTime GetDateTime()
        {
            return timeSync.GetDateTime();
        }
        public Task<bool> SyncTime()
        {
           return timeSync.SyncTime();
        }

        public void Disconnect()
        {
            sslClient.Disconnect();
        }

        private void HandleDisconnected()
        {
            IsConnected = false;

            timeSync.StopAutoTimeSync();
            Disconnected?.Invoke();
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;
            try
            {
                sslClient?.Dispose();
                timeSync?.Dispose();
            }
            catch { }
            
        }
    }
}
