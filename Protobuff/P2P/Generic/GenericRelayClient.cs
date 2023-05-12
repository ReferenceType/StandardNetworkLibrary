//using MessageProtocol;
//using NetworkLibrary;
//using NetworkLibrary.Components;
//using NetworkLibrary.Components.Statistics;
//using NetworkLibrary.MessageProtocol;
//using NetworkLibrary.MessageProtocol.Serialization;
//using NetworkLibrary.Utils;
//using Protobuff.Components;
//using Protobuff.P2P.Generic.Components;
//using Protobuff.P2P.Generic.HolePunch;
//using Protobuff.P2P.Generic.Interfaces.Messages;
//using Protobuff.P2P.HolePunch;
//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.Net.Security;
//using System.Runtime.CompilerServices;
//using System.Security.Cryptography.X509Certificates;
//using System.Text;
//using System.Threading.Tasks;

//namespace Protobuff.P2P.Generic
//{
//    class GenericUdpP2PChannels<E,S>
//        where E : IMessageEnvelope, new()
//        where S : ISerializer, new()
//    {
//        public GenericSecureUdpMessageClient<E, S> ch1;
//        public GenericSecureUdpMessageClient<E, S> ch2;

//        public GenericUdpP2PChannels(GenericSecureUdpMessageClient<E,S> ch1, GenericSecureUdpMessageClient<E, S> ch2)
//        {
//            this.ch1 = ch1;
//            this.ch2 = ch2;
//        }

//        public void Set(GenericSecureUdpMessageClient<E, S> cl)
//        {
//            if (ch1 == null)
//                ch1 = cl;
//            else ch2 = cl;
//        }
//    }
//    public class GenericRelayClient<E,ET,C,S>
//           where E : IMessageEnvelope, new()
//           where ET: IEndpointTransferMessage<ET>, new()
//           where C : IChanneCreationMessage, new()
//           where S : ISerializer, new()
//    {
//        public Action<Guid> OnPeerRegistered;
//        public Action<Guid> OnPeerUnregistered;
//        public Action<E> OnUdpMessageReceived;
//        public Action<E> OnMessageReceived;
//        public Action OnDisconnected;
//        public bool IsConnected { get => isConnected; private set => isConnected = value; }
//        public RemoteCertificateValidationCallback RemoteCertificateValidationCallback => protoClient.RemoteCertificateValidationCallback;

//        private readonly SecureMessageClient<E,S> protoClient;

//        private GenericMessageSerializer<E,S> serialiser = new GenericMessageSerializer<E, S>();
//        public Guid sessionId { get; private set; }
//        public ConcurrentDictionary<Guid, bool> Peers = new ConcurrentDictionary<Guid, bool>();

//        private ConcurrentDictionary<Guid, GenericSecureUdpMessageClient<E,S>> holePunchCandidates
//            = new ConcurrentDictionary<Guid, GenericSecureUdpMessageClient<E, S>>();

//        private ConcurrentDictionary<Guid, GenericUdpP2PChannels<E,S>> directUdpClients
//            = new ConcurrentDictionary<Guid, GenericUdpP2PChannels<E, S>>();

//        private ConcurrentDictionary<Guid, TaskCompletionSource<bool>> awaitingUdpPunchMessage
//            = new ConcurrentDictionary<Guid, TaskCompletionSource<bool>>();

//        private GenericSecureUdpMessageClient<E, S> udpRelayClient;

//        private bool connecting;
        
//        internal ConcurrentDictionary<Guid, PeerInfo> PeerInfos { get; private set; } = new ConcurrentDictionary<Guid, PeerInfo>();

//        internal string connectHost;
//        internal int connectPort;
//        private GenericPingHandler<E> pinger = new GenericPingHandler<E>();
//        private GenericClientHolepunchStateManager<E,ET,C,S> holepunchManager = new GenericClientHolepunchStateManager<E, ET, C, S>();
//        private object registeryLocker = new object();
//        private bool isConnected;

//        public GenericRelayClient(X509Certificate2 clientCert)
//        {
//            protoClient = CreateClient(clientCert);
//            protoClient.OnMessageReceived += HandleMessageReceived;
//            protoClient.OnDisconnected += HandleDisconnect;

//        }

//        protected virtual SecureMessageClient<E, S> CreateClient(X509Certificate2 clientCert)
//        {
//            return new SecureMessageClient<E, S>(clientCert);
//        }

//        public void StartPingService(int intervalMs = 1000)
//        {
//            Task.Run(() => SendPing(intervalMs));
//        }


//        #region Connect & Disconnect

//        [MethodImpl(MethodImplOptions.NoInlining)]
//        public void Connect(string host, int port)
//        {
//            if (connecting || IsConnected) return;

//            connectHost = host;
//            connectPort = port;
//            connecting = true;
//            try
//            {
//                protoClient.Connect(host, port);
//                RegisterRoutine();
//                IsConnected = true;
//                pinger.PeerRegistered(sessionId);

//            }
//            catch { throw; }
//            finally
//            {
//                connecting = false;
//            }



//        }

//        [MethodImpl(MethodImplOptions.NoInlining)]
//        public async Task<bool> ConnectAsync(string host, int port)
//        {
//            if (connecting || IsConnected) return false;

//            try
//            {
//                connecting = true;

//                connectHost = host;
//                connectPort = port;
//                await protoClient.ConnectAsync(host, port).ConfigureAwait(false);

//                var requestRegistery = new E();
//                requestRegistery.Header = InternalMessageResources.RequestRegistery;
//                requestRegistery.IsInternal = true;

//                var response = await protoClient.SendMessageAndWaitResponse(requestRegistery, 20000).ConfigureAwait(false);

//                connecting = false;


//                if (response.Header == MessageEnvelope.RequestTimeout ||
//                    response.Header == InternalMessageResources.RegisteryFail)
//                    throw new TimeoutException(response.Header);

//                this.sessionId = response.To;
//                IsConnected = true;
//                pinger.PeerRegistered(sessionId);

//                return true;
//            }
//            catch
//            {
//                throw;
//            }
//            finally
//            {
//                connecting = false;
//            }


//        }
//        public void Disconnect()
//        {
//            protoClient?.Disconnect();
//            udpRelayClient?.Dispose();
//            foreach (var item in directUdpClients)
//            {
//                item.Value.ch1.Dispose();
//                item.Value.ch2.Dispose();
//            }
//        }

//        private void HandleDisconnect()
//        {
//            lock (registeryLocker)
//            {
//                foreach (var peer in PeerInfos)
//                {
//                    OnPeerUnregistered?.Invoke(peer.Key);
//                }
//                PeerInfos = new ConcurrentDictionary<Guid, PeerInfo>();
//                Peers.Clear();
//                directUdpClients.Clear();
//                OnDisconnected?.Invoke();
//                IsConnected = false;
//            }

//        }

//        #endregion

//        protected virtual void RegisterRoutine()
//        {
//            var requestRegistery = new E();
//            requestRegistery.Header = InternalMessageResources.RequestRegistery;
//            requestRegistery.IsInternal = true;

//            var response = protoClient.SendMessageAndWaitResponse(requestRegistery, 20000).Result;

//            if (response.Header == MessageEnvelope.RequestTimeout ||
//                response.Header == InternalMessageResources.RegisteryFail)
//                throw new TimeoutException(response.Header);

//            this.sessionId = response.To;
//        }

//        #region Ping
//        private async void SendPing(int intervalMs)
//        {
//            while (true)
//            {
//                await Task.Delay(intervalMs / 2).ConfigureAwait(false);

//                E msg = new E();
//                msg.Header = PingHandler.Ping;
//                if (IsConnected)
//                {
//                    msg.TimeStamp = DateTime.Now;
//                    msg.From = sessionId;
//                    msg.To = sessionId;

//                    protoClient.SendAsyncMessage(msg);
//                    SendUdpMesssage(sessionId, msg);
//                    pinger.NotifyTcpPingSent(sessionId, msg.TimeStamp);
//                    pinger.NotifyUdpPingSent(sessionId, msg.TimeStamp);

//                    await Task.Delay(intervalMs / 2).ConfigureAwait(false);
//                    foreach (var peer in Peers.Keys)
//                    {
//                        msg.TimeStamp = DateTime.Now;
//                        SendAsyncMessage(peer, msg);
//                        pinger.NotifyTcpPingSent(peer, msg.TimeStamp);


//                        msg.TimeStamp = DateTime.Now;
//                        SendUdpMesssage(peer, msg);
//                        pinger.NotifyUdpPingSent(peer, msg.TimeStamp);

//                    }
//                }



//            }

//        }

//        private void HandlePing(E message, bool isTcp = true)
//        {
//            // self ping - server roundtrip.
//            if (message.From == sessionId)
//            {
//                //HandlePong(message,isTcp);
//                if (isTcp) pinger.HandleTcpPongMessage(message);
//                else pinger.HandleUdpPongMessage(message);
//            }
//            else
//            {
//                message.Header = PingHandler.Pong;
//                if (isTcp)
//                {
//                    message.To = message.From;
//                    message.From = sessionId;
//                    protoClient.SendAsyncMessage(message);
//                }
//                else
//                    SendUdpMesssage(message.From, message);
//            }


//        }

//        private void HandlePong(E message, bool isTcp = true)
//        {
//            if (isTcp) pinger.HandleTcpPongMessage(message);
//            else pinger.HandleUdpPongMessage(message);
//        }

//        public Dictionary<Guid, double> GetTcpPingStatus()
//        {
//            return pinger.GetTcpLatencies();
//        }
//        public Dictionary<Guid, double> GetUdpPingStatus()
//        {
//            return pinger.GetUdpLatencies();
//        }

//        #endregion Ping

//        #region Send
//        public void SendUdpMesssage<T>(Guid toId, T message, string messageHeader = null, int channel = 0) where T:class
//        {
//            if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
//                return;
//            E envelope = new E();
//            envelope.From = sessionId;
//            envelope.To = toId;
//            envelope.Header = messageHeader == null ? typeof(T).Name : messageHeader;

//            if (directUdpClients.TryGetValue(toId, out var client))
//            {
//                if (channel == 0)
//                    client.ch1.SendAsyncMessage(envelope, message);
//                else
//                    client.ch2.SendAsyncMessage(envelope, message);

//            }
//            else
//                udpRelayClient.SendAsyncMessage(envelope, message);
//        }

//        public void SendUdpMesssage(Guid toId, E message, int channel = 0)
//        {
//            if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
//                return;
//            message.From = sessionId;
//            message.To = toId;

//            if (directUdpClients.TryGetValue(toId, out var client))
//            {
//                if (channel == 0)
//                    client.ch1.SendAsyncMessage(message);
//                else
//                    client.ch2.SendAsyncMessage(message);
//            }

//            else
//                udpRelayClient.SendAsyncMessage(message);
//        }
//        public void SendUdpMesssage<T>(Guid toId, E message, T innerMessage, int channel = 0) 
//        {
//            if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
//                return;

//            message.From = sessionId;
//            message.To = toId;

//            if (directUdpClients.TryGetValue(toId, out var client))
//            {
//                if (channel == 0)
//                    client.ch1.SendAsyncMessage(message, innerMessage);
//                else
//                    client.ch2.SendAsyncMessage(message, innerMessage);
//            }

//            else
//                udpRelayClient.SendAsyncMessage(message, innerMessage);
//        }
//        public void SendUdpMesssage(Guid toId, byte[] data, int offset, int count, string dataName, int channel = 0)
//        {
//            if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
//                return;

//            E envelope = new E();
//            envelope.From = sessionId;
//            envelope.To = toId;
//            envelope.Header = dataName;

//            if (directUdpClients.TryGetValue(toId, out var client))
//            {
//                if (channel == 0)
//                    client.ch1.SendAsyncMessage(envelope, data, offset, count);
//                else
//                    client.ch2.SendAsyncMessage(envelope, data, offset, count);
//            }

//            else
//                udpRelayClient.SendAsyncMessage(envelope, data, offset, count);
//        }

//        public void SendAsyncMessage(Guid toId, E message)
//        {
//            if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
//                return;

//            message.From = sessionId;
//            message.To = toId;
//            protoClient.SendAsyncMessage(message);
//        }
//        public void SendAsyncMessage<T>(Guid toId, T message, string messageHeader = null) 
//        {
//            if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
//                return;

//            var envelope = new E()
//            {
//                From = sessionId,
//                To = toId,
//            };
//            envelope.Header = messageHeader == null ? typeof(T).Name : messageHeader;
//            protoClient.SendAsyncMessage(envelope, message);
//        }

//        public void SendAsyncMessage<T>(Guid toId, E envelope, T message) 
//        {
//            if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
//                return;

//            envelope.From = sessionId;
//            envelope.To = toId;
//            protoClient.SendAsyncMessage(envelope, message);
//        }

//        public void SendAsyncMessage(Guid toId, byte[] data, string dataName)
//        {
//            if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
//                return;

//            var envelope = new E()
//            {
//                From = sessionId,
//                To = toId,
//            };
//            envelope.Header = dataName;
//            protoClient.SendAsyncMessage(envelope, data, 0, data.Length);
//        }

//        public void SendAsyncMessage(Guid toId, byte[] data, int offset, int count, string dataName)
//        {
//            if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
//                return;

//            var envelopedMessage = new E()
//            {
//                From = sessionId,
//                To = toId,
//            };
//            envelopedMessage.Header = dataName;
//            protoClient.SendAsyncMessage(envelopedMessage, data, offset, count);
//        }

//        public Task<E> SendRequestAndWaitResponse<T>(Guid toId, T message, string messageHeader = null, int timeoutMs = 10000) 
//        {
//            var envelope = new E()
//            {
//                MessageId = Guid.NewGuid(),
//                From = sessionId,
//                To = toId,
//                Header = messageHeader == null ? typeof(T).Name : messageHeader
//            };
           
//            var task = protoClient.SendMessageAndWaitResponse(envelope, message, timeoutMs);
//            return task;
//        }

//        public Task<E> SendRequestAndWaitResponse(Guid toId, byte[] data, string dataName, int timeoutMs = 10000)
//        {
//            var envelope = new E()
//            {
//                MessageId = Guid.NewGuid(),
//                From = sessionId,
//                To = toId,
//                Header = dataName
//            };

//            var response = protoClient.SendMessageAndWaitResponse(envelope, data, 0, data.Length, timeoutMs = 10000);
//            return response;
//        }
//        public Task<E> SendRequestAndWaitResponse(Guid toId, E message, int timeoutMs = 10000)
//        {
//            message.From = sessionId;
//            message.To = toId;

//            var response = protoClient.SendMessageAndWaitResponse(message, timeoutMs);
//            return response;
//        }
//        public Task<E> SendRequestAndWaitResponse(Guid toId, E message, byte[] buffer, int offset, int count, int timeoutMs = 10000)
//        {
//            message.From = sessionId;
//            message.To = toId;

//            var response = protoClient.SendMessageAndWaitResponse(message, buffer, offset, count, timeoutMs);
//            return response;
//        }

//        public Task<E> SendRequestAndWaitResponse<T>(Guid toId, E envelope, T message, int timeoutMs = 10000) 
//        {
//            envelope.From = sessionId;
//            envelope.To = toId;

//            var response = protoClient.SendMessageAndWaitResponse(envelope, message, timeoutMs);
//            return response;
//        }

//        #endregion

//        private void HandleMessageReceived(E message)
//        {
//            if (message.IsInternal)
//            {
//                if (holepunchManager.HandleMessage(message))
//                    return;

//                switch (message.Header)
//                {
//                    case HolepunchHeaders.CreateChannel:
//                        HandleHoplePunchChannelCreation(message);
//                        break;

//                    case InternalMessageResources.RegisteryAck:
//                        SendRegisteryFinalisationMsg(message);
//                        break;

//                    case InternalMessageResources.UdpInit:
//                        message.LockBytes();
//                        CreateUdpChannel(message);
//                        break;

//                    case InternalMessageResources.UdpInitResend:
//                        ResendUdpInitMessage(message);
//                        break;

//                    case InternalMessageResources.UdpFinaliseInit:
//                        message.LockBytes();
//                        FinalizeUdpInit(message);
//                        break;
//                    case (InternalMessageResources.NotifyPeerListUpdate):
//                        UpdatePeerList(message);
//                        break;

//                    case PingHandler.Ping:
//                        HandlePing(message);
//                        break;

//                    case PingHandler.Pong:
//                        HandlePong(message);
//                        break;
//                    default:
//                        OnMessageReceived?.Invoke(message);
//                        break;
//                }

//            }
//            else
//            {
//                switch (message.Header)
//                {
//                    case PingHandler.Ping:
//                        HandlePing(message);
//                        break;

//                    case PingHandler.Pong:
//                        HandlePong(message);
//                        break;

//                    default:
//                        OnMessageReceived?.Invoke(message);
//                        break;

//                }

//            }

//        }

//        private async void SendRegisteryFinalisationMsg(E message)
//        {
//            await Task.Delay(20).ConfigureAwait(false);
//            int attemps = 500;
//            while (connecting && attemps > 0)
//            {
//                await Task.Delay(20).ConfigureAwait(false);
//                attemps--;
//            }
//            try
//            {
//                protoClient.SendAsyncMessage(message);

//            }
//            catch
//            {

//            }


//        }

//        private void HandleHoplePunchChannelCreation(E message)
//        {
//            holepunchManager.CreateChannel(this, message)
//                   .ContinueWith((result, nill) =>
//                   {
//                       var a = result.Result;
//                       GenericClientHolepunchState<E,ET,C,S> state = result.GetAwaiter().GetResult();
//                       if (state != null)
//                       {
//                           var client = state.holepunchClient;
//                           client.OnMessageReceived = null;
//                           client.OnMessageReceived += HandleUdpMessageReceived;
//                           if (directUdpClients.TryGetValue(state.DestinationId, out var ch))
//                           {
//                               ch.Set(client);
//                           }
//                           else
//                               directUdpClients[state.DestinationId] = new GenericUdpP2PChannels<E, S>(client, null);
//                       }
//                   }, null);
//        }



//        #region Relay Udp Channel Creation
//        private void CreateUdpChannel(E message)
//        {
//            ConcurrentAesAlgorithm algo = new ConcurrentAesAlgorithm(message.Payload, message.Payload);
//            udpRelayClient = new GenericSecureUdpMessageClient<E, S>(algo);
//            udpRelayClient.SocketSendBufferSize = 1280000;
//            udpRelayClient.ReceiveBufferSize = 12800000;
//            udpRelayClient.OnMessageReceived += HandleUdpMessageReceived;
//            udpRelayClient.Bind();
//            udpRelayClient.SetRemoteEnd(connectHost, connectPort);

//            message.From = message.To;
//            message.Payload = null;
//            message.IsInternal = true;

//            var bytes = serialiser.SerializeMessageEnvelope(message);
//            udpRelayClient.SendAsync(bytes);

//            protoClient.SendAsyncMessage(message);
//            Console.WriteLine("Created channel responding..");

//        }


//        private void ResendUdpInitMessage(E message)
//        {
//            message.Header = InternalMessageResources.UdpInit;
//            message.From = message.To;
//            message.Payload = null;
//            message.IsInternal = true;

//            MiniLogger.Log(MiniLogger.LogLevel.Info, "Resending Udp Init to " + udpRelayClient.RemoteEndPoint.ToString());
//            udpRelayClient.SendAsyncMessage(message);
//            protoClient.SendAsyncMessage(message);
//        }

//        private void FinalizeUdpInit(E message)
//        {
//            message.LockBytes();
//            message.IsInternal = true;
//            ConcurrentAesAlgorithm algo = new ConcurrentAesAlgorithm(message.Payload, message.Payload);
//            udpRelayClient.SwapAlgorith(algo);
//            protoClient.SendAsyncMessage(message);
//        }

//        #endregion

//        #region Hole Punch
//        public bool RequestHolePunch(Guid peerId, int timeOut = 10000, bool encrypted = true)
//        {
//            return RequestHolePunchAsync(peerId, timeOut, encrypted).Result;
//        }
//        // Ask the server about holepunch
//        public async Task<bool> RequestHolePunchAsync(Guid peerId, int timeOut, bool encrypted = true)
//        {
//            bool ret = false;
//            for (int i = 0; i < 2; i++)
//            {
//                var udpClient = await holepunchManager.CreateHolepunchRequest(this, peerId, timeOut, encrypted).ConfigureAwait(false);
//                if (udpClient != null)
//                {
//                    udpClient.OnMessageReceived = null;
//                    udpClient.OnMessageReceived += HandleUdpMessageReceived;
//                    if (directUdpClients.TryGetValue(peerId, out var ch))
//                    {
//                        ch.Set(udpClient);
//                    }
//                    else
//                        directUdpClients[peerId] = new GenericUdpP2PChannels<E, S>(udpClient, null);
//                    MiniLogger.Log(MiniLogger.LogLevel.Info, "Sucessfully punched hole");
//                    ret = true;
//                }
//                else
//                {
//                    MiniLogger.Log(MiniLogger.LogLevel.Info, "Hole Punch Failed");
//                    ret = false;
//                    break;
//                }
//            }
//            return ret;


//        }

//        #endregion

//        internal void HandleUdpMessageReceived(E message)
//        {
//            if (message.Header.Equals(HolepunchHeaders.HoplePunch)) { }
//            else if (message.Header.Equals(PingHandler.Ping)) HandlePing(message, isTcp: false);
//            else if (message.Header.Equals(PingHandler.Pong)) HandlePong(message, isTcp: false);

//            else OnUdpMessageReceived?.Invoke(message);
//        }

//        public PeerInfo GetPeerInfo(Guid peerId)
//        {
//            PeerInfos.TryGetValue(peerId, out var val);
//            return val;
//        }

//        protected virtual void UpdatePeerList(E message)
//        {
//            lock (registeryLocker)
//            {
//                PeerList serverPeerInfo = null;
//                if (message.Payload == null)
//                    serverPeerInfo = new PeerList() { PeerIds = new Dictionary<Guid, PeerInfo>() };
//                else
//                {
//                    serverPeerInfo = serialiser.Deserialize<PeerList>
//                        (message.Payload, message.PayloadOffset, message.PayloadCount);

//                }

//                foreach (var peer in Peers.Keys)
//                {
//                    if (!serverPeerInfo.PeerIds.ContainsKey(peer))
//                    {
//                        Peers.TryRemove(peer, out _);
//                        OnPeerUnregistered?.Invoke(peer);
//                        pinger.PeerUnregistered(peer);
//                        PeerInfos.TryRemove(peer, out _);
//                    }
//                }

//                foreach (var peer in serverPeerInfo.PeerIds.Keys)
//                {
//                    if (!Peers.TryGetValue(peer, out _))
//                    {
//                        Peers.TryAdd(peer, true);
//                        OnPeerRegistered?.Invoke(peer);
//                        pinger.PeerRegistered(peer);
//                        PeerInfos.TryAdd(peer, serverPeerInfo.PeerIds[peer]);
//                    }
//                }

//            }
//        }

//        public void GetTcpStatistics(out TcpStatistics stats) => protoClient.GetStatistics(out stats);
//    }
//}
