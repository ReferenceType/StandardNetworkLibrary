using NetworkLibrary;
using NetworkLibrary.Components;
using NetworkLibrary.Components.Statistics;
using NetworkLibrary.MessageProtocol;
using NetworkLibrary.MessageProtocol.Serialization;
using NetworkLibrary.UDP;
using NetworkLibrary.Utils;
using Protobuff.P2P.HolePunch;
using Protobuff.P2P.StateManagemet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Protobuff.P2P
{
    public class SecureProtoRelayServer : SecureProtoMessageServer, INetworkNode
    {
        private AsyncUdpServer udpServer;
        private ConcurrentProtoSerialiser serialiser = new ConcurrentProtoSerialiser();

        private ConcurrentDictionary<Guid, string> RegisteredPeers = new ConcurrentDictionary<Guid, string>();
        private ConcurrentDictionary<Guid, IPEndPoint> RegisteredUdpEndpoints = new ConcurrentDictionary<Guid, IPEndPoint>();
        private ConcurrentDictionary<Guid, List<EndpointData>> ClientUdpEndpoints = new ConcurrentDictionary<Guid, List<EndpointData>>();
        private ConcurrentDictionary<IPEndPoint, ConcurrentAesAlgorithm> UdpCryptos = new ConcurrentDictionary<IPEndPoint, ConcurrentAesAlgorithm>();

        private TaskCompletionSource<bool> PushPeerList = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool shutdown = false;
        private ConcurrentAesAlgorithm relayDectriptor;
       // private ServerHolepunchStateManager sm = new ServerHolepunchStateManager();
        internal byte[] ServerUdpInitKey { get; }
        //private ServerConnectionStateManager connectionStateManager;
        private ServerStateManager stateManager;
        public SecureProtoRelayServer(int port, X509Certificate2 cerificate) : base(port, cerificate)
        {
            udpServer = new AsyncUdpServer(port);

            ServerUdpInitKey = new Byte[16];
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            rng.GetNonZeroBytes(ServerUdpInitKey);
            relayDectriptor = new ConcurrentAesAlgorithm(ServerUdpInitKey, ServerUdpInitKey);

            udpServer.OnClientAccepted += UdpClientAccepted;
            udpServer.OnBytesRecieved += HandleUdpBytesReceived;
            stateManager = new ServerStateManager(this);

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
                catch (Exception ex) { PushPeerList.TrySetResult(true); }

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
            SendAsyncMessage(clientId, peerlistMsg, (stream)=>KnownTypeSerializer.SerializePeerList(stream,listProto));
          //  SendAsyncMessage(in clientId, peerlistMsg, listProto);
        }


        #endregion Push Updates

        #region Registration

       
        protected override void HandleClientAccepted(Guid clientId)
        {
           // connectionStateManager.CreateState(Guid.NewGuid(), clientId);
        }



        internal void Register(Guid clientId, IPEndPoint remoteEndpoint,List<EndpointData> localEndpoints, byte[] random)
        {
            UdpCryptos.TryAdd(remoteEndpoint, new ConcurrentAesAlgorithm(random, random));
            RegisteredUdpEndpoints.TryAdd(clientId, remoteEndpoint);

            localEndpoints.Add(new EndpointData(remoteEndpoint));
            ClientUdpEndpoints.TryAdd(clientId, localEndpoints);

            RegisterPeer(in clientId);
        }

        private void RegisterPeer(in Guid clientId)
        {
            RegisteredPeers[clientId] = null;
            PushPeerList.TrySetResult(true);
        }

        #endregion Registration

        protected override void HandleClientDisconnected(Guid clientId)
        {
            RegisteredPeers.TryRemove(clientId, out _);
            if (RegisteredUdpEndpoints.TryRemove(clientId, out var key))
            {
                UdpCryptos.TryRemove(key, out _);
                udpServer.RemoveClient(key);
            }

            PushPeerList.TrySetResult(true);
        }

        #region Receive
        // Goal is to only read envelope and route with that, without unpcaking payload.
        protected override void OnBytesReceived(in Guid guid, byte[] bytes, int offset, int count)
        {
            try
            {
                var message = serialiser.DeserialiseOnlyRouterHeader(bytes, offset, count);
                if (message.IsInternal)
                {
                    var messageEnvelope = serialiser.DeserialiseEnvelopedMessage(bytes, offset, count);
                    HandleMessageReceived(guid, messageEnvelope);
                }
                else server.SendBytesToClient(message.To, bytes, offset, count);

            }
            catch (Exception e)
            {
                MiniLogger.Log(MiniLogger.LogLevel.Error, "While deserializing envelope, an error occured: " +
                    e.Message);
            }

        }

        void HandleMessageReceived(Guid clientId, MessageEnvelope message)
        {
            if (//!connectionStateManager.HandleMessage(message)
                //&&!sm.HandleMessage(message)
                // && 
               !stateManager.HandleMessage(clientId,message) &&
                !CheckAwaiter(message))
            {
                switch (message.Header)
                {
                    //case Constants.Register:
                    //    connectionStateManager.CreateState(Guid.NewGuid(), clientId);
                    //    break;
                    //case (Constants.HolePunchRequest):
                    //    sm.CreateState(this, message);
                    //    break;
                    case Constants.GetEndpoints:
                        HandleEndpointTransfer(message);
                        break;
                    case "WhatsMyIp":
                        SendEndpoint(clientId, message);
                        break;

                    default:
                         server.SendAsyncMessage(message.To, message);
                        break;

                }
            }
        }

        private void HandleEndpointTransfer(MessageEnvelope message)
        {
            byte[] cryptoKey=null;
            if (message.KeyValuePairs!=null && message.KeyValuePairs.TryGetValue("Encrypted",out _))
            {
                var random = new byte[16];
                RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
                rng.GetNonZeroBytes(random);
                cryptoKey = random;
            }
            if (ClientUdpEndpoints.TryGetValue(message.To, out var endpointsD) &&
                ClientUdpEndpoints.TryGetValue(message.From, out var endpointsR))
            {
                // requester
                SendAsyncMessage(message.From, new MessageEnvelope()
                {
                    IsInternal= true,
                    From = message.To,
                    To = message.From,
                    MessageId = message.MessageId,
                    Header = Constants.EndpointTransfer
                },
                    (stream) => KnownTypeSerializer.SerializeEndpointTransferMessage(stream,
                                new EndpointTransferMessage() {
                                    IpRemote=cryptoKey,
                                    LocalEndpoints = endpointsD }));
                // destination
                SendAsyncMessage(message.To, new MessageEnvelope()
                {
                    IsInternal = true,
                    From = message.From,
                    To = message.To,
                    MessageId = message.MessageId,
                    Header = Constants.EndpointTransfer
                },
                  (stream) => KnownTypeSerializer.SerializeEndpointTransferMessage(stream,
                              new EndpointTransferMessage() { IpRemote = cryptoKey, LocalEndpoints = endpointsR }));
            }
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

        private void UdpClientAccepted(SocketAsyncEventArgs ClientSocket)
        {
        }


        private void HandleUdpBytesReceived(IPEndPoint adress, byte[] bytes, int offset, int count)
        {
            //filter unknown messages
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
         
            var buffer = BufferPool.RentBuffer(count + 256);
            try
            {
                int decrptedAmount = crypto.DecryptInto(bytes, offset, count, buffer, 0);

                // only read header to route the message.
                var message = serialiser.DeserialiseOnlyRouterHeader(buffer, 1, decrptedAmount-1);
                if (RegisteredUdpEndpoints.TryGetValue(message.To, out var destEp))
                {
                    if (UdpCryptos.TryGetValue(destEp, out var encryptor))
                    {
                        var reEncryptedBytesAmount = encryptor.EncryptInto(buffer, 0, decrptedAmount, buffer, 0);
                        udpServer.SendBytesToClient(destEp, buffer, 0, reEncryptedBytesAmount);
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
                BufferPool.ReturnBuffer(buffer);
            }
        }

        private void HandleUnregistreredMessage(IPEndPoint adress, byte[] bytes, int offset, int count)
        {
            try
            {
                byte[] result = relayDectriptor.Decrypt(bytes, offset, count);
                var message1 = serialiser.DeserialiseEnvelopedMessage(result, 1, result.Length-1);
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
    }
}
