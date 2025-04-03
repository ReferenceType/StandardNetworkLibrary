using NetworkLibrary.Components.Crypto;
using NetworkLibrary.Components.Crypto.DiffieHellman;
using NetworkLibrary.Components.Crypto.KeyDerivation;
using NetworkLibrary.DistributedP2P.Channels;
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

namespace NetworkLibrary.DistributedP2P.Client
{
    public class DistributedLobbyClient<S> : IDistributedConnection where S : ISerializer, new()
    {
        IClientDbConnection clientDbConnector;
        IClientAuthenticationProvider clientAuthProvider;
        SecureMessageClient<S> sslClient;
        StateManager stateManager = new StateManager();
        TimeSync timeSync;

        private ConcurrentDictionary<Guid, PeerStatus> onlinePeers = new ConcurrentDictionary<Guid, PeerStatus>();

        public event Action<IChannel> PeerConnected;
        public event Action<PeerStatus> PeerOnline;
        public event Action<PeerStatus> PeerOffline;

        public event Action<MessageEnvelope> MessageReceived;
        public event Action Disconnected;

        public Guid SessionId { get; private set; }

        private int connected = 0;
        public bool IsConnected 
        {
            get => Interlocked.CompareExchange(ref connected, 0, 0) == 1;
            private set => Interlocked.Exchange(ref connected, value ? 1 : 0); 
        }

        public DistributedLobbyClient(IClientDbConnection clientDbConnector,
                                      IClientAuthenticationProvider clientAuthProvider,
                                      X509Certificate2 certificate = null)
        {
            this.clientDbConnector = clientDbConnector;
            this.clientAuthProvider = clientAuthProvider;
            sslClient = new SecureMessageClient<S>(certificate);
            sslClient.OnMessageReceived += HandleServerMsg;
            sslClient.OnDisconnected += HandleDisconnected;
            timeSync = new TimeSync(this);
        }



        public async Task<bool> ConnectAsync(string ip, int port)
        {
            if (IsConnected)
                return true;

            IClientAuthenticationToken authToken = clientAuthProvider.Authenticate();

            bool res = await sslClient.ConnectAsync(ip, port);
            if (res)
            {
                Guid conversationId = Guid.NewGuid();
                var conState = new ClientConnectionState(conversationId, this, clientDbConnector, authToken);
                stateManager.RegisterState(conState);
                conState.Start();

                await conState.WaitCompletion();

                if (conState.IsSuccesful)
                {
                    SessionId = conState.SessionId;
                    IsConnected = true;
                    timeSync.StartAutoTimeSync(5000);
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

        // we dont need this socket methods afeterall
        public async Task<Socket> OpenTcpSocket(Guid destinationPeer,string socketName)
        {
            var Info = new ChannelInfo();
            Info.ChannelName = socketName;
            Info.ChannelType = ChannelType.RawTcp;

            var pipeState = new ClientPipeState(Guid.NewGuid(), this, Info);
            stateManager.RegisterState(pipeState);
            pipeState.Start(destinationPeer, tcp: true);

            await pipeState.WaitCompletion();

            if (pipeState.IsSuccesful)
            {
                Console.WriteLine("PipeSuccesfull");
                return pipeState.ConnectedSocket;
            }
            return null;
        }

        public async Task<Socket> OpenUdpSocket(Guid destinationPeer, string socketName)
        {
            var Info = new ChannelInfo();
            Info.ChannelName = socketName;
            Info.ChannelType = ChannelType.RawUdp;

            var pipeState = new ClientPipeState(Guid.NewGuid(), this,Info);
            stateManager.RegisterState(pipeState);
            pipeState.Start(destinationPeer, tcp:false);

            await pipeState.WaitCompletion();

            if (pipeState.IsSuccesful)
            {
                Console.WriteLine("PipeSuccesfull");
                return pipeState.ConnectedSocket;
            }
            return null;
        }


        public async Task<IChannel> OpenTcpChannel(Guid destinationPeer, ChannelInfo Info)
        {
            var pipeState = new ClientPipeState(Guid.NewGuid(), this, Info);
            stateManager.RegisterState(pipeState);
            pipeState.Start(destinationPeer,tcp: true);

            await pipeState.WaitCompletion();

            if (pipeState.IsSuccesful)
            {
                IChannel channel = CreateChannel(pipeState);
                return channel;
            }
            return null;
        }

        private void HandlePipeCreated(IConversationState state)
        {
            if (state.IsSuccesful)
            {
                var pipeState = (ClientPipeState)state;
                IChannel channel = CreateChannel(pipeState);

                if (channel != null)
                    PeerConnected?.Invoke(channel);

                Console.WriteLine("DestPeer Conn Succesfull");
                // notify that a connection is opened, like socket accept
            }
        }

        private static IChannel CreateChannel(ClientPipeState pipeState)
        {
            IChannel channel = null;
            switch (pipeState.ChannelInfo.ChannelType)
            {
                case ChannelType.RawTcp:
                    channel = new RawTcpSocket(pipeState.ChannelInfo, pipeState.ConnectedSocket);
                    break;
                case ChannelType.RawUdp:
                    channel = new RawUdpSocket(pipeState.ChannelInfo, pipeState.ConnectedSocket);
                    break;
                case ChannelType.ByteMessage:
                    channel = new ByteMessageChannel(pipeState.ChannelInfo, pipeState.ConnectedSocket);
                    break;
                case ChannelType.SecureByteMessage:
                    var symetricKey = HKDFLite.DeriveKey(pipeState.sharedSecret,outputLength:16);
                    var algo = new NetworkLibrary.Components.ConcurrentAesAlgorithm(symetricKey,AesMode.GCM);
                    AesTcpClient client = new AesTcpClient(algo,pipeState.ConnectedSocket);
                    channel = new SecureByteMessageChannel(client, pipeState.ChannelInfo);
                    break;
            }

            return channel;
        }

        private async Task<byte[]> PerformDHWithPeer(Guid destinationPeer)
        {

            DiffieHellman df = new DiffieHellman();
            byte[] publicKey = df.GetPublicKey();

            MessageEnvelope envelope = new MessageEnvelope();
            envelope.Header = "DH";
            envelope.To = destinationPeer;
            envelope.Payload = publicKey;

            var response = await SendMessageAndWaitResponse(envelope);
            if (response.Header != MessageEnvelope.RequestTimeout)
            {
                response.LockBytes();
                byte[] dstPublic = response.Payload;

                var secret = df.CalculateSharedSecret(dstPublic);
                var symetricKey = HKDFLite.DeriveKey(secret, outputLength: 16);
                return symetricKey;
            }
            return null;
        }

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

                        var pipeState = new ClientPipeState(envelope, this);
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

        public async Task<bool> TryUdpHolePunch(Guid destination) 
        {
            var state = new ClientUdpHolepunchState(Guid.NewGuid(), destination, this);
            stateManager.RegisterState(state);
            state.Start();

            await state.WaitCompletion();

            //if (state.IsSuccesful)
            //{
            //    state.Socket.SendTo( new byte[689], state.SuccesfulEndpoint);
            //}
            return state.IsSuccesful;
        }

        private void ManageUdpHolepunchRequest(MessageEnvelope envelope)
        {
            var state = new ClientUdpHolepunchState(envelope.MessageId, envelope.From, this);
            stateManager.RegisterState(state);
            state.OnComplete += State_OnComplete;
            state.HandleMessage(envelope);

            void State_OnComplete(IConversationState obj)
            {
                if (state.IsSuccesful)
                {
                    //byte[] buff =  new byte[1024];
                    //int rec = state.Socket.Receive(buff);
                    //Console.WriteLine("YIPPIE");
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


    }
}
