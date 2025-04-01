using NetworkLibrary.Components.Crypto.DiffieHellman;
using NetworkLibrary.Components.Crypto.KeyDerivation;
using NetworkLibrary.DistributedP2P.Channels;
using NetworkLibrary.DistributedP2P.Client.StateManagement;
using NetworkLibrary.DistributedP2P.Components;
using NetworkLibrary.DistributedP2P.Server;
using NetworkLibrary.MessageProtocol;
using NetworkLibrary.P2P.Components.HolePunch;
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

        public event Action<ITcpChannel> PeerConnected;
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
                    timeSync = new TimeSync(this);
                    await timeSync.SyncTime();
                    timeSync.StartAutoTimeSync(5000);
                    return true;
                }

                return false;

            }
            else return false;
        }

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
        public async Task<Socket> OpenTcpSocket(Guid destinationPeer, ChannelInfo Info)
        {
            var pipeState = new ClientPipeState(Guid.NewGuid(), this);
            stateManager.RegisterState(pipeState);
            pipeState.Start(destinationPeer);

            await pipeState.WaitCompletion();

            if (pipeState.IsSuccesful)
            {
                Console.WriteLine("PipeSuccesfull");
                return pipeState.ConnectedSocket;
            }
            return null;
        }


        public async Task<ITcpChannel> OpenTcpChannel(Guid destinationPeer, ChannelInfo Info)
        {
            var pipeState = new ClientPipeState(Guid.NewGuid(), this);
            stateManager.RegisterState(pipeState);
            pipeState.Start(destinationPeer);

            await pipeState.WaitCompletion();

            if (pipeState.IsSuccesful)
            {
                var info = new ChannelInfo();
                var channel = new ByteMessageChannel(info, pipeState.ConnectedSocket);
                return channel;


                Console.WriteLine("PipeSuccesfull");
                return null;
                var symetricKey = await PerformDHWithPeer(destinationPeer);
                if (symetricKey != null)
                {
                    //var channel = new SecureTcpChannel(Info, pipeState.ConnectedSocket, symetricKey);
                    return null;
                }
            }
            return null;
        }

        private void HandlePipeCreated(IConversationState state)
        {
            if (state.IsSuccesful)
            {
                var pipeState = (ClientPipeState)state;

                var info = new ChannelInfo();
                var channel = new ByteMessageChannel(info, pipeState.ConnectedSocket);
                PeerConnected?.Invoke(channel);

                Console.WriteLine("DestPeer Conn Succesfull");
                // notify that a connection is opened, like socket accept
            }
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
                    case InternalConstants.PipeTokenDeliveryTcp:

                        var pipeState = new ClientPipeState(envelope.MessageId, this);
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

        private int disposed = 0;


        public double GetTime()
        {
            return timeSync.GetTime();
        }

        public DateTime GetDateTime()
        {
            return timeSync.GetDateTime();
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

        DateTime ITimeProvider.GetTime()
        {
           return timeSync.GetDateTime();
        }
    }
}
