using NetworkLibrary.Components;
using NetworkLibrary.UDP;
using NetworkLibrary.Utils;
using NetworkLibrary;
using Protobuff.P2P.HolePunch;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MessageProtocol;
using Protobuff.P2P.Generic.Interfaces.Messages;
using Protobuff.P2P.Generic.HolePunch;
using NetworkLibrary.Components.Statistics;
using System.Linq;

namespace Protobuff.P2P.Generic
{
    public class GenericRelayServer<E, ET, C, S,R> : SecureMessageServer<E,S>
           where E : IMessageEnvelope, new()
           where ET : IEndpointTransferMessage<ET>, new()
           where C : IChanneCreationMessage, new()
           where S : ISerializer, new()
           where R : IRouterHeader, new()
    {
        private AsyncUdpServer udpServer;
        private GenericMessageSerializer<E,S> serialiser = new GenericMessageSerializer<E, S>();
        private ConcurrentDictionary<Guid, string> RegisteredPeers = new ConcurrentDictionary<Guid, string>();
        private ConcurrentDictionary<Guid, IPEndPoint> RegisteredUdpEndpoints = new ConcurrentDictionary<Guid, IPEndPoint>();
        private ConcurrentDictionary<IPEndPoint, ConcurrentAesAlgorithm> UdpCryptos = new ConcurrentDictionary<IPEndPoint, ConcurrentAesAlgorithm>();
        private ConcurrentDictionary<Guid, IPEndPoint> HolePunchers = new ConcurrentDictionary<Guid, IPEndPoint>();
        private TaskCompletionSource<bool> PushPeerList = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool shutdown = false;
        private ConcurrentAesAlgorithm relayDectriptor;
        private GenericServerHolepunchStateManager<E,ET,C,S,R> sm = new GenericServerHolepunchStateManager<E, ET, C,S,R>();

        internal byte[] ServerUdpInitKey { get; }


        public GenericRelayServer(int port, X509Certificate2 cerificate) : base(port, cerificate)
        {
            udpServer = new AsyncUdpServer(port);

            udpServer.OnClientAccepted += UdpClientAccepted;
            udpServer.OnBytesRecieved += HandleUdpBytesReceived;
            udpServer.StartServer();

            Task.Run(PeerListPushRoutine);

            ServerUdpInitKey = new Byte[16];
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            rng.GetNonZeroBytes(ServerUdpInitKey);
            relayDectriptor = new ConcurrentAesAlgorithm(ServerUdpInitKey, ServerUdpInitKey);
            OnClientDisconnected += HandleClientDisconnected;
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
                PushPeerList = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
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

            var peerlistMsg = new E();
            peerlistMsg.Header = InternalMessageResources.NotifyPeerListUpdate;
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
                        IP = GetIPEndPoint(peer.Key).Address.ToString(),
                        Port = GetIPEndPoint(peer.Key).Port
                    };
                }
            }

            var listProto = new PeerList<PeerInfo>();
            listProto.PeerIds = peerList;
            SendAsyncMessage(in clientId, peerlistMsg, listProto);
        }


        #endregion Push Updates

        #region Registration

        private void InitiateRegisteryRoutine(Guid clientId, E message)
        {
            Task.Run(async () =>
            {
                try
                {
                    if (VerifyClientCredentials(clientId, message))
                    {
                        bool res = await GenerateSeureUdpChannel(clientId).ConfigureAwait(false);
                        if (res == false)
                            return;

                        E reply = new E();
                        reply.Header = InternalMessageResources.RegisterySucces;
                        reply.To = clientId;
                        reply.MessageId = message.MessageId;
                        reply.IsInternal = true;

                        SendAsyncMessage(clientId, reply);

                        E requestAck = new E();
                        requestAck.Header = InternalMessageResources.RegisteryAck;
                        requestAck.IsInternal = true;
                        requestAck.To = clientId;

                        var ackResponse = await SendMessageAndWaitResponse(clientId, requestAck, 10000).ConfigureAwait(false);

                        if (ackResponse.Header != MessageEnvelope.RequestTimeout)
                        {
                            RegisterPeer(in clientId);
                        }
                    }
                    else
                    {
                        E reply = new E();
                        reply.Header = InternalMessageResources.RegisteryFail;
                        reply.To = clientId;
                        reply.MessageId = message.MessageId;
                        reply.IsInternal = true;

                        SendAsyncMessage(in clientId, reply);
                    }
                }
                catch (Exception ex)
                {
                    MiniLogger.Log(MiniLogger.LogLevel.Error,
                        "Registration routine encountered an error: " + ex.Message);
                }

            });


        }

        protected virtual async Task<bool> GenerateSeureUdpChannel(Guid clientId)
        {
            var requestUdpRegistration = new E();
            requestUdpRegistration.Header = InternalMessageResources.UdpInit;
            requestUdpRegistration.To = clientId;
            requestUdpRegistration.Payload = ServerUdpInitKey;
            requestUdpRegistration.IsInternal = true;

            var udpREsponse = await SendMessageAndWaitResponse(clientId, requestUdpRegistration, 10000).ConfigureAwait(false);
            if (udpREsponse.Header.Equals(MessageEnvelope.RequestTimeout)) return false;

            IPEndPoint ClientEp = null;
            if (!RegisteredUdpEndpoints.TryGetValue(clientId, out ClientEp))
            {
                var resentInitMessage = new E();
                resentInitMessage.Header = InternalMessageResources.UdpInitResend;
                resentInitMessage.To = clientId;
                resentInitMessage.IsInternal = true;

                int tries = 0;
                while (!RegisteredUdpEndpoints.TryGetValue(clientId, out ClientEp) && ClientEp == null && tries < 500)
                {
                    udpREsponse = await SendMessageAndWaitResponse(clientId, resentInitMessage, 10000).ConfigureAwait(false);
                    if (udpREsponse.Header.Equals(MessageEnvelope.RequestTimeout)) return false;

                    tries++;
                    await Task.Delay(1).ConfigureAwait(false);
                }

                if (tries > 100)
                {
                    return false;
                }
            }

            var random = new byte[16];
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            rng.GetNonZeroBytes(random);

            var udpFinalize = new E();
            udpFinalize.Header = InternalMessageResources.UdpFinaliseInit;
            udpFinalize.To = clientId;
            udpFinalize.Payload = random;
            udpFinalize.IsInternal = true;

            UdpCryptos[ClientEp] = new ConcurrentAesAlgorithm(random, random);

            udpREsponse = await SendMessageAndWaitResponse(clientId, udpFinalize, 10000).ConfigureAwait(false);
            if (udpREsponse.Header.Equals(MessageEnvelope.RequestTimeout))
                return false;
            return true;

        }
        protected virtual bool VerifyClientCredentials(Guid clientId, E message)
        {
            return true;
        }

        private void RegisterPeer(in Guid clientId)
        {
            RegisteredPeers[clientId] = null;
            PushPeerList.TrySetResult(true);
        }

        #endregion Registration

        protected void HandleClientDisconnected(Guid clientId)
        {
            RegisteredPeers.TryRemove(clientId, out _);
            if (RegisteredUdpEndpoints.TryRemove(clientId, out var key))
            {
                UdpCryptos.TryRemove(key, out _);
                udpServer.RemoveClient(key);
            }

            PushPeerList.TrySetResult(true);
        }

        //protected override void HandleClientAccepted(Guid clientId)
        //{
        //    // todo 
        //    // countdown client registration time drop if necessary

        //}
       

        #region Receive
        // Goal is to only read envelope and route with that, without unpcaking payload.
        protected override void HandleBytesReceived(Guid guid, byte[] bytes, int offset, int count)
        {
            try
            {
                var message = serialiser.DeserialiseOnlyRouterHeader<R>(bytes, offset, count);
                if (message.IsInternal)
                {
                    var messageEnvelope = serialiser.DeserialiseEnvelopedMessage(bytes, offset, count);
                    HandleMessageReceived(guid, messageEnvelope);
                }
                else SendBytesToClient(message.To, bytes, offset, count);

            }
            catch (Exception e)
            {
                MiniLogger.Log(MiniLogger.LogLevel.Error, "While deserializing envelope, an error occured: " +
                    e.Message);
            }

        }

        bool HandleMessageReceived(Guid clientId, E message)
        {

            if (!sm.HandleMessage(message)
                && !CheckAwaiter(message))
            {
                switch (message.Header)
                {
                    case (InternalMessageResources.RequestRegistery):
                        InitiateRegisteryRoutine(clientId, message);
                        return true;
                    case (HolepunchHeaders.HolePunchRequest):
                        sm.CreateState(this, message);
                        return true;
                    case "WhatsMyIp":
                        SendEndpoint(clientId, message);
                        return true;

                    default:
                        return false;
                }
            }
            return true;

        }
        #endregion

        private void SendEndpoint(Guid clientId, E message)
        {
            var info = new PeerInfo()
            {
                IP = GetIPEndPoint(clientId).Address?.ToString(),
                Port = GetIPEndPoint(clientId).Port
            };

            var env = new E();
            env.IsInternal = true;
            env.MessageId = message.MessageId;
            SendAsyncMessage(clientId, env, info);
        }

        private void UdpClientAccepted(SocketAsyncEventArgs ClientSocket)
        {

        }


        private void HandleUdpBytesReceived(IPEndPoint adress, byte[] bytes, int offset, int count)
        {
            // Client is trying to register its Udp endpoint here
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
                var message = serialiser.DeserialiseOnlyRouterHeader<R>(buffer, 0, decrptedAmount);
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
            byte[] result = null;
            try
            {
                result = relayDectriptor.Decrypt(bytes, offset, count);
                var message1 = serialiser.DeserialiseEnvelopedMessage(result, 0, result.Length);
                sm.HandleUdpMessage(adress, message1);

                if (message1.Header != null && message1.Header.Equals(InternalMessageResources.UdpInit))
                {
                    RegisteredUdpEndpoints.TryAdd(message1.From, adress);
                }
            }
            catch
            {
                MiniLogger.Log(MiniLogger.LogLevel.Error, "Udp Relay failed to decrypt unregistered peer message");
            }
        }
    }
}
