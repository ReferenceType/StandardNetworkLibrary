using NetworkLibrary.Components;
using NetworkLibrary.Components.Crypto;
using NetworkLibrary.Components.Statistics;
using NetworkLibrary.MessageProtocol;
using NetworkLibrary.P2P.Components;
using NetworkLibrary.P2P.Components.HolePunch;
using NetworkLibrary.P2P.Components.StateManagement;
using NetworkLibrary.P2P.Components.StateManagement.Server;
using NetworkLibrary.UDP;
using NetworkLibrary.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkLibrary.P2P.Generic
{
    public class SecureRelayServerBase<S> : SecureMessageServer<S>, INetworkNode where S : ISerializer, new()
    {
        private AsyncUdpServer udpServer;
        internal GenericMessageSerializer<S> serialiser = new GenericMessageSerializer<S>();

        private ConcurrentDictionary<Guid, string> RegisteredPeers = new ConcurrentDictionary<Guid, string>();
        private ConcurrentDictionary<Guid, IPEndPoint> RegisteredUdpEndpoints = new ConcurrentDictionary<Guid, IPEndPoint>();
        private ConcurrentDictionary<Guid, List<EndpointData>> ClientUdpEndpoints = new ConcurrentDictionary<Guid, List<EndpointData>>();
        private ConcurrentDictionary<IPEndPoint, ConcurrentAesAlgorithm> UdpCryptos = new ConcurrentDictionary<IPEndPoint, ConcurrentAesAlgorithm>();
        internal ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, string>> peerReachabilityMatrix
           = new ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, string>>();
        private TaskCompletionSource<bool> PushPeerList = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool shutdown = false;
        private ConcurrentAesAlgorithm relayDectriptor;
        internal byte[] ServerUdpInitKey { get; }
        private ServerStateManager<S> stateManager;
        private readonly byte[] serverNameBytes;
        public AesMode AESMode;
        bool initialised = false;
        int port = 0;
        public SecureRelayServerBase(int port, X509Certificate2 certificate, string serverName = "Andromeda") : base(port, certificate)
        {
            this.port = port;
            serverNameBytes = Encoding.UTF8.GetBytes(serverName);

            ServerUdpInitKey = new byte[16];
            var rng = RandomNumberGenerator.Create();
            rng.GetNonZeroBytes(ServerUdpInitKey);
            rng.Dispose();
        }
        public SecureRelayServerBase(int port, string serverName = "Andromeda") : base(port)
        {
            this.port = port;
            serverNameBytes = Encoding.UTF8.GetBytes(serverName);

            ServerUdpInitKey = new byte[16];
            var rng = RandomNumberGenerator.Create();
            rng.GetNonZeroBytes(ServerUdpInitKey);
            rng.Dispose();
        }
        public override void StartServer()
        {
            Initialise();
            base.StartServer();
        }
        private void Initialise()
        {
            if(initialised) return;
            initialised=true;

            OnClientAccepted += HandleClientAccepted;
            //OnBytesReceived += HandleTCPReceived;
            OnClientDisconnected += HandleClientDisconnected;
            udpServer = new AsyncUdpServer(port);

            relayDectriptor = new ConcurrentAesAlgorithm(ServerUdpInitKey, AESMode);

            udpServer.OnClientAccepted += UdpClientAccepted;
            udpServer.OnBytesRecieved += HandleUdpBytesReceived;
            stateManager = new ServerStateManager<S>(this);

            udpServer.StartServer();

            Task.Run(PeerListPushRoutine);
        }

        public void GetTcpStatistics(out TcpStatistics generalStats, out ConcurrentDictionary<Guid, TcpStatistics> sessionStats)
           => GetStatistics(out generalStats, out sessionStats);

        public void GetUdpStatistics(out UdpStatistics generalStats, out ConcurrentDictionary<IPEndPoint, UdpStatistics> sessionStats)
        {
            udpServer.GetStatistics(out generalStats, out sessionStats);
        }

        public bool TryGetClientId(IPEndPoint ep, out Guid id)
        {
            id = RegisteredUdpEndpoints.Where(x => x.Value == ep).Select(x => x.Key).FirstOrDefault();
            return id != default;
        }

        #region Push Updates
        private async Task PeerListPushRoutine()
        {
            while (!shutdown)
            {
                await PushPeerList.Task.ConfigureAwait(false);
                Interlocked.Exchange(ref PushPeerList, new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
                try
                {
                    foreach (var item in RegisteredPeers)
                    {
                        NotifyCurrentPeerList(item.Key);
                    }
                }
                catch { PushPeerList.TrySetResult(true); }

                await Task.Delay(1000).ConfigureAwait(false);

            }
        }

        // this needs to be overridable
        protected virtual void NotifyCurrentPeerList(Guid clientId)
        {
            var peerlistMsg = new MessageEnvelope();
            peerlistMsg.Header = Constants.NotifyPeerListUpdate;
            peerlistMsg.IsInternal = true;

            var peerList = new Dictionary<Guid, PeerInfo>();
            // enumerating concurrent dict suppose to be thread safe but 
            // peer KV pair here can sometimes be null... 
            foreach (var peer in RegisteredPeers)
            {
                // exclude self
                if (!peer.Key.Equals(clientId))
                {
                    peerList[peer.Key] = new PeerInfo()
                    {
                        Address = GetIPEndPoint(peer.Key).Address.GetAddressBytes(),
                        Port = (ushort)GetIPEndPoint(peer.Key).Port
                    };
                }
            }

            var listProto = new PeerList();
            listProto.PeerIds = peerList;
            SendAsyncMessage(clientId, peerlistMsg, (stream) => KnownTypeSerializer.SerializePeerList(stream, listProto));
        }


        #endregion Push Updates

        #region Registration


        protected void HandleClientAccepted(Guid clientId)
        {
            Task.Delay(20000).ContinueWith((t) => { if (!RegisteredPeers.ContainsKey(clientId)) CloseSession(clientId); });
        }

        internal void Register(Guid clientId, IPEndPoint remoteEndpoint, List<EndpointData> localEndpoints, byte[] random)
        {
            UdpCryptos.TryAdd(remoteEndpoint, new ConcurrentAesAlgorithm(random, AESMode));
            RegisteredUdpEndpoints.TryAdd(clientId, remoteEndpoint);

            localEndpoints.Add(new EndpointData(remoteEndpoint));
            ClientUdpEndpoints.TryAdd(clientId, localEndpoints);
            RegisteredPeers[clientId] = null;

            PublishPeerRegistered(clientId);
        }

        protected virtual void PublishPeerRegistered(Guid clientId)
        {
            PushPeerList.TrySetResult(true);
        }
        protected virtual void PublishPeerUnregistered(Guid clientId)
        {
            PushPeerList.TrySetResult(true);
        }

        #endregion Registration

        protected virtual void HandleClientDisconnected(Guid clientId)
        {
            RegisteredPeers.TryRemove(clientId, out _);
            if (RegisteredUdpEndpoints.TryRemove(clientId, out var key))
            {
                UdpCryptos.TryRemove(key, out _);
                udpServer.RemoveClient(key);
            }

            PublishPeerUnregistered(clientId);
        }

        #region Receive
        // Goal is to only read envelope and route with that, without unpcaking payload.
        protected override void HandleBytes(Guid guid, byte[] bytes, int offset, int count)
        {
            try
            {
                var message = serialiser.DeserialiseOnlyRouterHeader(bytes, offset, count);
                if (message.IsInternal)
                {
                    var messageEnvelope = serialiser.DeserialiseEnvelopedMessage(bytes, offset, count);
                    HandleMessageReceivedInternal(guid, messageEnvelope);
                }
                else if (message.To == Guid.Empty)
                {
                    BroadcastMessage(guid, bytes, offset, count);
                }
                else SendBytesToClient(message.To, bytes, offset, count);

            }
            catch (Exception e)
            {
                MiniLogger.Log(MiniLogger.LogLevel.Error, "While deserializing envelope, an error occured: " +
                    e.Message);
            }

        }

        protected virtual void BroadcastMessage(Guid guid, byte[] bytes, int offset, int count)
        {
            foreach (var peerId in RegisteredPeers.Keys)
            {
                if (peerId != guid)
                    SendBytesToClient(peerId, bytes, offset, count);
            }
        }

        protected virtual void HandleMessageReceivedInternal(Guid clientId, MessageEnvelope message)
        {
            if (!stateManager.HandleMessage(clientId, message) &&
                !CheckAwaiter(message))
            {
                switch (message.Header)
                {

                    case Constants.InitiateHolepunch:
                        InitiateHolepunchBetweenPeers(message);
                        break;
                    case Constants.NotifyServerHolepunch:
                        HandleHolepunchcompletion(message);
                        break;
                    case "WhatsMyIp":
                        SendEndpoint(clientId, message);
                        break;

                    default:
                        SendAsyncMessage(message.To, message);
                        break;
                }
            }
        }

        private void InitiateHolepunchBetweenPeers(MessageEnvelope message)
        {
            byte[] cryptoKey = null;
            if (message.KeyValuePairs != null && message.KeyValuePairs.TryGetValue("Encrypted", out _))
            {
                var random = new byte[16];
                var rng = RandomNumberGenerator.Create();
                rng.GetNonZeroBytes(random);
                rng.Dispose();
                cryptoKey = random;
            }
            if (ClientUdpEndpoints.TryGetValue(message.To, out var endpointsD) &&
                ClientUdpEndpoints.TryGetValue(message.From, out var endpointsR))
            {

                // requester
                var mreq =  new MessageEnvelope()
                {
                    IsInternal = true,
                    From = message.To,
                    To = message.From,
                    MessageId = message.MessageId,
                    Header = Constants.InitiateHolepunch
                };

                SendAsyncMessage(message.From,mreq,
                    (stream) => KnownTypeSerializer.SerializeEndpointTransferMessage(stream,
                                new EndpointTransferMessage()
                                {
                                    IpRemote = cryptoKey,
                                    LocalEndpoints = endpointsD
                                }));


                // destination
                var mdest = new MessageEnvelope()
                {
                    IsInternal = true,
                    From = message.From,
                    To = message.To,
                    MessageId = message.MessageId,
                    Header = Constants.InitiateHolepunch
                };

              
                SendAsyncMessage(message.To, mdest,
                  (stream) => KnownTypeSerializer.SerializeEndpointTransferMessage(stream,
                              new EndpointTransferMessage() { 
                                  IpRemote = cryptoKey,
                                  LocalEndpoints = endpointsR 
                              }));
            }
        }



        private void HandleHolepunchcompletion(MessageEnvelope message)
        {
            peerReachabilityMatrix.TryAdd(message.From, new ConcurrentDictionary<Guid, string>());
            peerReachabilityMatrix[message.From].TryAdd(message.To, null);
        }

        #endregion

        private void SendEndpoint(Guid clientId, MessageEnvelope message)
        {
            var info = new PeerInfo()
            {
                Address = GetIPEndPoint(clientId).Address.GetAddressBytes(),
                Port = (ushort)GetIPEndPoint(clientId).Port
            };

            var env = new MessageEnvelope();
            env.IsInternal = true;
            env.MessageId = message.MessageId;
            SendAsyncMessage(clientId, env, info);
        }

        private void UdpClientAccepted(SocketAsyncEventArgs ClientSocket) { }


        [ThreadStatic]
        static byte[] udpBuffer;
        static byte[] GetTlsBuffer()
        {
            if (udpBuffer == null)
                udpBuffer = ByteCopy.GetNewArray(65000, true);
            return udpBuffer;
        }

        [ThreadStatic]
        static byte[] udpBuffer2;
        static byte[] GetTlsBuffer2()
        {
            if (udpBuffer2 == null)
                udpBuffer2 = ByteCopy.GetNewArray(65000, true);
            return udpBuffer2;
        }

        [ThreadStatic]
        static byte[] udpBuffer3;
        static byte[] GetTlsBuffer3()
        {
            if (udpBuffer3 == null)
                udpBuffer3 = ByteCopy.GetNewArray(65000, true);
            return udpBuffer3;
        }


        private void HandleUdpBytesReceived(IPEndPoint adress, byte[] bytes, int offset, int count)
        {
            //filter unknown messages
            // maybe we can ban ip here
            if (!UdpCryptos.ContainsKey(adress))
            {
                HandleUnregistreredMessage(adress, bytes, offset, count);
                return;
            }

            // we need to find the crypto of sender and critptto of receiver
            // decrpy(key of .From) read relay header, encrypt(Key of .To)
            if (!UdpCryptos.TryGetValue(adress, out var crypto))
            {
                return;
            }

            //  var udpBuffer = BufferPool.RentBuffer(count + 256);
           var udpBuffer = GetTlsBuffer();
            try
            {
                int decrptedAmount = crypto.DecryptInto(bytes, offset, count, udpBuffer, 0);

                // only read header to route the message.
                var message = serialiser.DeserialiseOnlyRouterHeader(udpBuffer, 0, decrptedAmount);
                if (message.To == Guid.Empty)
                {
                    BroadcastUdp(udpBuffer, decrptedAmount);

                }
                else if (RegisteredUdpEndpoints.TryGetValue(message.To, out var destEp))
                {
                    if (UdpCryptos.TryGetValue(destEp, out var encryptor))
                    {
                        var encBuffer = GetTlsBuffer3();
                        var reEncryptedBytesAmount = encryptor.EncryptInto(udpBuffer, 0, decrptedAmount, encBuffer, 0);
                        udpServer.SendBytesToClient(destEp, encBuffer, 0, reEncryptedBytesAmount);
                    }
                }
            }
            catch (Exception e)
            {
                MiniLogger.Log(MiniLogger.LogLevel.Error, "Udp Relay failed to deserialise envelope message " + e.Message);
                return;
            }
            finally
            {
                //  BufferPool.ReturnBuffer(udpBuffer);
            }
        }
        protected void RelayUdpMessage(Guid clientId, byte[] unecrypted, int offset, int count)
        {
            if (RegisteredUdpEndpoints.TryGetValue(clientId, out var destEp))
            {
                if (UdpCryptos.TryGetValue(destEp, out var encryptor))
                {
                    var buffer = GetTlsBuffer2();
                    var reEncryptedBytesAmount = encryptor.EncryptInto(unecrypted, offset, count, buffer, 0);
                    udpServer.SendBytesToClient(destEp, buffer, 0, reEncryptedBytesAmount);

                }
            }
        }

        protected virtual void BroadcastUdp(byte[] buffer, int decrptedAmount)
        {
            var message = serialiser.DeserialiseEnvelopedMessage(buffer, 0, decrptedAmount);
            peerReachabilityMatrix.TryGetValue(message.From, out var map);

            // here filter the holepunch stuff.
            foreach (var item in RegisteredUdpEndpoints)
            {
                if (map != null && map.TryGetValue(item.Key, out _))
                {
                    continue;
                }
                if (item.Key == message.From)
                    continue;
               // message.To = item.Key;
                var destEp = item.Value;
                var tempBuff = GetTlsBuffer2();
                if (UdpCryptos.TryGetValue(destEp, out var encryptor))
                {
                    var reEncryptedBytesAmount = encryptor.EncryptInto(buffer, 0, decrptedAmount, tempBuff, 0);
                    udpServer.SendBytesToClient(destEp, tempBuff, 0, reEncryptedBytesAmount);
                }
            }

        }

        private void HandleUnregistreredMessage(IPEndPoint adress, byte[] bytes, int offset, int count)
        {
            if(count == 1 && bytes[offset] == 91)
            {
                udpServer.SendBytesToClient(adress, serverNameBytes,0,serverNameBytes.Length);
                return;
            }
            if (bytes[offset] == 92 && bytes[offset+1] == 93)
            {
                try
                {
                    var message1 = serialiser.DeserialiseEnvelopedMessage(bytes, offset+2, count);
                    stateManager.HandleMessage(adress, message1);
                }
                catch (Exception e)
                {
                    MiniLogger.Log(MiniLogger.LogLevel.Error, "Error occured during tcp holepunch portmap: "+e.StackTrace);
                }
                return;
                
            }
          
            try
            {
                byte[] result = relayDectriptor.Decrypt(bytes, offset, count);
                var message1 = serialiser.DeserialiseEnvelopedMessage(result, 0, result.Length);
                stateManager.HandleMessage(adress, message1);
                
            }
            catch
            {
                MiniLogger.Log(MiniLogger.LogLevel.Error, "Udp Relay failed to decrypt unregistered peer message");
            }
          
        }

        void INetworkNode.SendUdpAsync(IPEndPoint ep, MessageEnvelope message, Action<PooledMemoryStream> callback, ConcurrentAesAlgorithm aesAlgorithm)
        {
            throw new NotImplementedException();
        }

        void INetworkNode.SendUdpAsync(IPEndPoint ep, MessageEnvelope message, Action<PooledMemoryStream> callback)
        {
            throw new NotImplementedException();
        }

        void INetworkNode.SendAsyncMessage(Guid destinatioinId, MessageEnvelope message)
        {
            SendAsyncMessage(destinatioinId, message);
        }

        public void SendAsyncMessage(MessageEnvelope message, Action<PooledMemoryStream> callback, Guid destinationId)
        {
            SendAsyncMessage(destinationId, message,callback);
        }
    }
}
