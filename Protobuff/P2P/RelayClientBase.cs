using NetworkLibrary.Components;
using NetworkLibrary.Utils;
using NetworkLibrary;
using Protobuff.Components;
using Protobuff.P2P.HolePunch;
using Protobuff.P2P.Modules;
using Protobuff.P2P.StateManagemet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using NetworkLibrary.Components.Statistics;
using Protobuff.P2P.StateManagemet.Client;
using System.Data;
using System.Security.Cryptography;
using NetworkLibrary.MessageProtocol;
using System.IO;

namespace Protobuff.P2P
{
    public class RelayClientBase<S>:INetworkNode
        where S: ISerializer, new()
    {
        public class PeerInformation
        {
            public string IP;
            public int Port;
            public IPAddress IPAddress;
            public PeerInformation(PeerInfo info)
            {
                IPAddress = new IPAddress(info.Address);
                IP = IPAddress.ToString();
                Port = info.Port;
            }
            public PeerInformation() {}
        }

        public Action<Guid> OnPeerRegistered;
        public Action<Guid> OnPeerUnregistered;
        public Action<MessageEnvelope> OnUdpMessageReceived;
        public Action<MessageEnvelope> OnMessageReceived;
        public Action OnDisconnected;
        public RemoteCertificateValidationCallback RemoteCertificateValidationCallback;

        [Obsolete("Use SessionId instead")]
        public Guid sessionId { get; private set; }
        public Guid SessionId => sessionId;

        public bool IsConnected { get => isConnected; private set => isConnected = value; }
        public ConcurrentDictionary<Guid, bool> Peers = new ConcurrentDictionary<Guid, bool>();
        internal ConcurrentDictionary<Guid, ReliableUdpModule> RUdpModules = new ConcurrentDictionary<Guid, ReliableUdpModule>();
        internal ConcurrentDictionary<Guid, PeerInformation> PeerInfos { get; private set; } = new ConcurrentDictionary<Guid, PeerInformation>();
       

        internal string connectHost;
        internal int connectPort;
        internal ClientUdpModule udpServer;
        internal IPEndPoint relayServerEndpoint;
        internal SecureMessageClient<S> tcpMessageClient;
        private ConcurrentAesAlgorithm udpEncriptor;

        private object registeryLocker = new object();
        private bool isConnected;
        private bool connecting;
        private PingHandler pinger = new PingHandler();
        private GenericMessageSerializer<S> serialiser = new GenericMessageSerializer<S>();
        private ConcurrentDictionary<Guid, IPEndPoint> punchedEndpoints = new ConcurrentDictionary<Guid, IPEndPoint>();
        private ConcurrentDictionary<IPEndPoint, ConcurrentAesAlgorithm> peerCryptos = new ConcurrentDictionary<IPEndPoint, ConcurrentAesAlgorithm>();
        private ClientStateManager<S> clientStateManager;
        public RelayClientBase(X509Certificate2 clientCert)
        {
            clientStateManager = new ClientStateManager<S>(this);
            tcpMessageClient = new SecureMessageClient<S>(clientCert);
            tcpMessageClient.OnMessageReceived += HandleMessageReceived;
            tcpMessageClient.OnDisconnected += HandleDisconnect;
            tcpMessageClient.RemoteCertificateValidationCallback += CertificateValidation;

            udpServer = new ClientUdpModule(0);
            udpServer.SocketReceiveBufferSize = 12800000;
            udpServer.SocketSendBufferSize = 12800000;
            udpServer.OnBytesRecieved += HandleUdpBytesReceived;
            udpServer.StartServer();
        }
        public void GetTcpStatistics(out TcpStatistics stats) => tcpMessageClient.GetStatistics(out stats);

        private bool CertificateValidation(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (RemoteCertificateValidationCallback == null)
                return true;
            return RemoteCertificateValidationCallback.Invoke(sender, certificate, chain, sslPolicyErrors);
        }

        #region Connect & Disconnect

        public async Task<bool> ConnectAsync(string host, int port)
        {
            if (connecting || IsConnected) return false;

            connectHost = host;
            connectPort = port;
            connecting = true;
            try
            {
                relayServerEndpoint = new IPEndPoint(IPAddress.Parse(connectHost), connectPort);

                await tcpMessageClient.ConnectAsync(host, port).ConfigureAwait(false);
                Console.WriteLine("Connected");

                var stateCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                clientStateManager.CreateConnectionState()
                .Completed += (Istate) =>
                { 
                    if(Istate.Status == StateStatus.Completed)
                    {
                        var state = Istate as ClientConnectionState;
                        sessionId = state.SessionId;
                        udpEncriptor = state.udpEncriptor;
                        tcpMessageClient.SendAsyncMessage(new MessageEnvelope()
                        {
                            IsInternal = true,
                            Header = Constants.ClientFinalizationAck,
                            MessageId = state.StateId
                        });

                        peerCryptos.TryAdd(relayServerEndpoint, udpEncriptor);

                        Volatile.Write(ref isConnected, true);
                        pinger.PeerRegistered(sessionId);

                        stateCompletion.SetResult(true);

                    }
                    else stateCompletion.TrySetException(new TimeoutException());
                };

                return await stateCompletion.Task;

            }
            catch { throw; }
            finally
            {
                connecting = false;
            }
        }
      
        internal List<EndpointData> GetLocalEndpoints()
        {
            List<EndpointData> endpoints = new List<EndpointData>();
            var lep = (IPEndPoint)udpServer.LocalEndpoint;

            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    if (ip.ToString() == "0.0.0.0")
                        continue;
                    endpoints.Add(new EndpointData()
                    {
                        Ip = ip.GetAddressBytes(),
                        Port = lep.Port
                    });
                }
            }
            return endpoints;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Connect(string host, int port)
        {
            var res = ConnectAsync(host, port).Result;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Disconnect()
        {
            tcpMessageClient?.Disconnect();
           
        }

        private void HandleDisconnect()
        {
            lock (registeryLocker)
            {
                foreach (var peer in PeerInfos)
                {
                    OnPeerUnregistered?.Invoke(peer.Key);
                }
                PeerInfos = new ConcurrentDictionary<Guid, PeerInformation>();
                Peers.Clear();

                peerCryptos.Clear();
                punchedEndpoints.Clear();

                OnDisconnected?.Invoke();
                IsConnected = false;
            }

        }

        #endregion

        #region Ping
        CancellationTokenSource cts;
        public void StartPingService(int intervalMs = 1000,
                                     bool sendToServer = true,
                                     bool sendTcpToPeers = true,
                                     bool sendUdpToPeers = true)
        {
            cts?.Cancel();
            cts =  new CancellationTokenSource();
            Task.Run(() => SendPing(intervalMs, sendToServer, sendTcpToPeers, sendUdpToPeers,cts.Token));
        }
        public void StopPingService()
        {
            cts?.Cancel();
            cts = null;
        }
        private async void SendPing(int intervalMs, bool sendToServer, bool sendTcpToPeers, bool sendUdpToPeers, CancellationToken token)
        {
            while (true)
            {
                if (token.IsCancellationRequested) return;
                await Task.Delay(intervalMs).ConfigureAwait(false);
                if(token.IsCancellationRequested) return;

                MessageEnvelope msg = new MessageEnvelope();
                msg.Header = Constants.Ping;
                if (IsConnected)
                {
                    var time = DateTime.Now;

                    if (sendToServer)
                    {
                        msg.From = sessionId;
                        msg.To = sessionId;

                        tcpMessageClient.SendAsyncMessage(msg);
                        pinger.NotifyTcpPingSent(sessionId, time);

                        SendUdpMesssage(sessionId, msg);
                        pinger.NotifyUdpPingSent(sessionId, time);
                    }

                    time = DateTime.Now;

                    if (sendTcpToPeers)
                        BroadcastMessage(msg);
                    if(sendUdpToPeers)
                        BroadcastUdpMessage(msg);
                    foreach (var peer in Peers.Keys)
                    {
                        if (sendTcpToPeers)
                            pinger.NotifyTcpPingSent(peer, time);
                        if (sendUdpToPeers)
                            pinger.NotifyUdpPingSent(peer, time);
                    }
                }
            }

        }

        private void HandlePing(MessageEnvelope message, bool isTcp = true)
        {
            // self ping - server roundtrip.
            if (message.From == sessionId)
            {
                //HandlePong(message,isTcp);
                if (isTcp) pinger.HandleTcpPongMessage(message);
                else pinger.HandleUdpPongMessage(message);
            }
            else
            {
                message.Header = Constants.Pong;
                if (isTcp)
                {
                    message.To = message.From;
                    message.From = sessionId;
                    tcpMessageClient.SendAsyncMessage(message);
                }
                else
                    SendUdpMesssage(message.From, message);
            }


        }

        private void HandlePong(MessageEnvelope message, bool isTcp = true)
        {
            if (isTcp) pinger.HandleTcpPongMessage(message);
            else pinger.HandleUdpPongMessage(message);
        }

        public Dictionary<Guid, double> GetTcpPingStatus()
        {
            return pinger.GetTcpLatencies();
        }
        public Dictionary<Guid, double> GetUdpPingStatus()
        {
            return pinger.GetUdpLatencies();
        }

        #endregion Ping

        #region Send

        private void SendUdpMesssageInternal(in Guid toId, MessageEnvelope message)
        {
           
            if (punchedEndpoints.TryGetValue(toId, out var endpoint))
            {
                udpServer.SendAsync(endpoint, message, peerCryptos[endpoint]);
            }
            else
                udpServer.SendAsync(relayServerEndpoint, message, udpEncriptor);
        }

        private void SendUdpMesssageInternal<T>(in Guid toId, MessageEnvelope message, T innerMessage) 
        {
            
            if (punchedEndpoints.TryGetValue(toId, out var endpoint))
            {
                udpServer.SendAsync(endpoint, message, innerMessage, peerCryptos[endpoint]);
            }
            else
                udpServer.SendAsync(relayServerEndpoint, message, innerMessage, udpEncriptor);
        }

        //public void SendUdpMesssage<T>(Guid toId, T message, string messageHeader = null, int channel = 0) 
        //{
        //    if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
        //        return;
        //    MessageEnvelope env = new MessageEnvelope();
        //    env.From = sessionId;
        //    env.To = toId;
        //    env.Header = messageHeader == null ? typeof(T).Name : messageHeader;

        //    SendUdpMesssageInternal(toId, env, message);
        //}

        public void SendUdpMesssage(Guid toId, MessageEnvelope message, int channel = 0)
        {
            if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
                return;
            message.From = sessionId;
            message.To = toId;


            SendUdpMesssageInternal(toId, message);
        }

        public void SendUdpMesssage<T>(Guid toId, MessageEnvelope message, T innerMessage, int channel = 0) 
        {
            if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
                return;

            message.From = sessionId;
            message.To = toId;


            SendUdpMesssageInternal(toId, message, innerMessage);
        }

        public void SendUdpMesssage(Guid toId, byte[] data, int offset, int count, string dataName, int channel = 0)
        {
            if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
                return;

            MessageEnvelope env = new MessageEnvelope();
            env.From = sessionId;
            env.To = toId;
            env.Header = dataName;
            env.SetPayload(data, offset, count);


            SendUdpMesssageInternal(toId, env);

        }

        public void BroadcastUdpMessage(MessageEnvelope message)
        {
            if (Peers.Count > 0)
            {
                message.From = sessionId;
                message.To = Guid.Empty;

                foreach (var item in punchedEndpoints)
                {
                    udpServer.SendAsync(item.Value, message, peerCryptos[item.Value]);
                }
                foreach (var item in Peers)
                {
                    if (!punchedEndpoints.ContainsKey(item.Key))
                    {
                        udpServer.SendAsync(relayServerEndpoint, message, udpEncriptor);
                        break;
                    }
                }
            }
        }
       
        public void BroadcastUdpMessage<T>(MessageEnvelope message, T innerMessage)
        {
            if (Peers.Count > 0)
            {
                message.From = sessionId;
                message.To = Guid.Empty;
                udpServer.SendAsync(relayServerEndpoint, message, udpEncriptor);

                foreach (var item in punchedEndpoints)
                {
                    udpServer.SendAsync(item.Value, message,innerMessage, peerCryptos[item.Value]);
                }
            }
        }

        internal void MulticastUdpMessage(MessageEnvelope message, ICollection<Guid> targets)
        {

            udpServer.SendAsync(relayServerEndpoint, message, udpEncriptor);

            foreach (var peerId in targets)
            {
                if (punchedEndpoints.TryGetValue(peerId, out var ep))
                    udpServer.SendAsync(ep, message, peerCryptos[ep]);

            }

        }
        internal void MulticastUdpMessage<T>(MessageEnvelope message, ICollection<Guid> targets, T innerMessage)
        {
          
            udpServer.SendAsync(relayServerEndpoint, message, udpEncriptor);

            foreach (var peerId in targets)
            {
                if (punchedEndpoints.TryGetValue(peerId, out var ep))
                    udpServer.SendAsync(ep, message, innerMessage, peerCryptos[ep]);
            }

        }

        public void BroadcastMessage(MessageEnvelope message)
        {
            if (Peers.Count > 0)
            {
                message.From = sessionId;
                message.To = Guid.Empty;
                tcpMessageClient.SendAsyncMessage(message);
            }
        }

        public void BroadcastMessage<T>(MessageEnvelope message, T innerMessage)
        {
            if (Peers.Count > 0)
            {
                message.From = sessionId;
                message.To = Guid.Empty;
                tcpMessageClient.SendAsyncMessage(message,innerMessage);
            }
        }

        public void SendAsyncMessage(Guid toId, MessageEnvelope message)
        {
            if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
                return;

            message.From = sessionId;
            message.To = toId;
            tcpMessageClient.SendAsyncMessage(message);
        }
        public void SendAsyncMessage<T>(Guid toId, MessageEnvelope envelope, T message) 
        {
            if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
                return;

            envelope.From = sessionId;
            envelope.To = toId;
            tcpMessageClient.SendAsyncMessage(envelope, message);
        }

        public void SendAsyncMessage(Guid toId, MessageEnvelope envelope, Action<PooledMemoryStream> serializationCallback)
        {
            if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
                return;

            envelope.From = sessionId;
            envelope.To = toId;
            tcpMessageClient.SendAsyncMessage(envelope, serializationCallback);
        }

        public void SendAsyncMessage<T>(Guid toId, T message, string messageHeader = null) 
        {
            if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
                return;

            var envelope = new MessageEnvelope()
            {
                From = sessionId,
                To = toId,
            };

            envelope.Header = messageHeader == null ? typeof(T).Name : messageHeader;
            tcpMessageClient.SendAsyncMessage(envelope, message);
        }

        public void SendAsyncMessage(Guid toId, byte[] data, string dataName)
        {
            if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
                return;

            var envelope = new MessageEnvelope()
            {
                From = sessionId,
                To = toId,
                Header = dataName
            };
            tcpMessageClient.SendAsyncMessage(envelope, data, 0, data.Length);
        }

        public void SendAsyncMessage(Guid toId, byte[] data, int offset, int count, string dataName)
        {
            if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
                return;

            var envelope = new MessageEnvelope()
            {
                From = sessionId,
                To = toId,
                Header = dataName
            };
            tcpMessageClient.SendAsyncMessage(envelope, data, offset, count);
        }

        public Task<MessageEnvelope> SendRequestAndWaitResponse<T>(Guid toId, T message, string messageHeader = null, int timeoutMs = 10000) where T : IProtoMessage
        {
            var envelope = new MessageEnvelope()
            {
                From = sessionId,
                To = toId,
                MessageId = Guid.NewGuid(),
                Header = messageHeader == null ? typeof(T).Name : messageHeader
            };

            var task = tcpMessageClient.SendMessageAndWaitResponse(envelope, message, timeoutMs);
            return task;
        }

        public Task<MessageEnvelope> SendRequestAndWaitResponse(Guid toId, byte[] data, string dataName, int timeoutMs = 10000)
        {
            var envelope = new MessageEnvelope()
            {
                From = sessionId,
                To = toId,
                MessageId = Guid.NewGuid(),
                Header = dataName
            };

            var response = tcpMessageClient.SendMessageAndWaitResponse(envelope, data, 0, data.Length, timeoutMs);
            return response;
        }
        public Task<MessageEnvelope> SendRequestAndWaitResponse(Guid toId, MessageEnvelope message, int timeoutMs = 10000)
        {
            message.From = sessionId;
            message.To = toId;

            var response = tcpMessageClient.SendMessageAndWaitResponse(message, timeoutMs);
            return response;
        }
        public Task<MessageEnvelope> SendRequestAndWaitResponse(Guid toId, MessageEnvelope message, byte[] buffer, int offset, int count, int timeoutMs = 10000)
        {
            message.From = sessionId;
            message.To = toId;

            var response = tcpMessageClient.SendMessageAndWaitResponse(message, buffer, offset, count, timeoutMs);
            return response;
        }

        public Task<MessageEnvelope> SendRequestAndWaitResponse<T>(Guid toId, MessageEnvelope envelope, T message, int timeoutMs = 10000) where T : IProtoMessage
        {
            envelope.From = sessionId;
            envelope.To = toId;

            var response = tcpMessageClient.SendMessageAndWaitResponse(envelope, message, timeoutMs);
            return response;
        }

        #endregion

        #region Receive

        private void HandleUdpBytesReceived(IPEndPoint adress, byte[] bytes, int offset, int count)
        {
            if (peerCryptos.TryGetValue(adress, out var crypto)/*!punchedEndpointsReverse.ContainsKey(adress)*/)
            {
                byte[] decryptBuffer = null;

                try
                {
                    MessageEnvelope msg;
                    if (crypto != null)
                    {
                         decryptBuffer = BufferPool.RentBuffer(count + 256);
                        int amountDecrypted = crypto.DecryptInto(bytes, offset, count, decryptBuffer, 0);
                        ParseMessage(decryptBuffer, 0, amountDecrypted);
                        BufferPool.ReturnBuffer(decryptBuffer);
                    }
                    else
                    {
                        ParseMessage(bytes, offset, count);
                    }

                    void ParseMessage(byte[] decryptedBytes, int byteOffset, int byteCount)
                    {
                       
                        msg = serialiser.DeserialiseEnvelopedMessage(decryptedBytes, byteOffset, byteCount);

                        if (!clientStateManager.HandleMessage(adress, msg))
                        {
                            HandleUdpMessageReceived(msg);
                        }
                        
                    }


                }
                catch (Exception e)
                {
                    var b = decryptBuffer;
                    MiniLogger.Log(MiniLogger.LogLevel.Error, "Relay Client Failed to deserialise envelope message " + e.Message);

                }
            }

        }
        private void HandleUdpMessageReceived(MessageEnvelope message)
        {
            if (message.Header == null) return;
            else if (message.Header.Equals(Constants.Ping)) HandlePing(message, isTcp: false);
            else if (message.Header.Equals(Constants.Pong)) HandlePong(message, isTcp: false);
            else if(message.Header== Constants.Rudp)
            {
                if(RUdpModules.TryGetValue(message.From, out var mod))
                {
                    mod.HandleBytes(message.Payload, message.PayloadOffset, message.PayloadCount);
                }
            }
            else HandleUdpMessage(message);
        }

        protected virtual void HandleUdpMessage(MessageEnvelope message)
        {
            OnUdpMessageReceived?.Invoke(message);
        }

        private void HandleMessageReceived(MessageEnvelope message)
        {

            if (message.IsInternal)
            {
                if (clientStateManager.HandleMessage(message))
                    return;
                else if(message.Header == Constants.NotifyPeerListUpdate)
                    UpdatePeerList(message);
                else
                    HandleMessage(message);
            }
            else
            {
                if (UdpAwaiter.IsWaiting(message.MessageId))
                {
                    UdpAwaiter.ResponseArrived(message);
                }
                switch (message.Header)
                {
                    case Constants.Ping:
                        HandlePing(message);
                        break;

                    case Constants.Pong:
                        HandlePong(message);
                        break;

                    default:
                        HandleMessage(message);
                        break;

                }

            }

        }

        protected virtual void HandleMessage(MessageEnvelope message)
        {
            OnMessageReceived?.Invoke(message);
        }

        #endregion

        #region Hole Punch
        public bool RequestHolePunch(Guid peerId, int timeOut = 10000, bool encrypted = true)
        {
            return RequestHolePunchAsync(peerId, timeOut, encrypted).Result;
        }

        public Task<bool> RequestHolePunchAsync(Guid peerId, int timeOut, bool encrypted = true)
        {
            if (clientStateManager.IsHolepunchStatePending(peerId) ||
                punchedEndpoints.ContainsKey(peerId))
            {
                return Task.FromResult(false);
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var state = clientStateManager.CreateHolePunchState(peerId, Guid.NewGuid());
            state.Completed += (s) =>
            {
                if (state.Status == StateStatus.Completed)
                {
                    tcs.SetResult(true);
                }
                else
                {
                    tcs.SetResult(false);
                }
            };
            return tcs.Task; 
        }

        // Callback from holepunch state.
        // This associates a crypto algorithm on an enpoint so we can decirpt the messages
        internal void RegisterCrypto(byte[] key, List<EndpointData> associatedEndpoints)
        {
            ConcurrentAesAlgorithm crypto = null;
            if (key != null)
                crypto = new ConcurrentAesAlgorithm(key, key);
            foreach (var item in associatedEndpoints)
            {
                peerCryptos.TryAdd(item.ToIpEndpoint(), crypto);
            }
        }

        // This is called on succesfull completion of a holepucnh
        internal void HandleHolepunchSuccess(ClientHolepunchState state)
        {
            foreach (var ep in state.targetEndpoints.LocalEndpoints)
            {
                var ipEp = ep.ToIpEndpoint();
                if (!ipEp.Equals(state.succesfulEpToReceive))
                {
                    peerCryptos.TryRemove(ipEp, out _);
                }
            }
            punchedEndpoints.TryAdd(state.destinationId, state.succesfulEpToReceive);

            MiniLogger.Log(MiniLogger.LogLevel.Info, $"Punched on {state.succesfulEpToReceive}");
        }

     
        internal void HandleHolepunchFailure(ClientHolepunchState state)
        {
            if(state.targetEndpoints!=null && state.targetEndpoints.LocalEndpoints != null)
            {
                var associatedEndpoints = state.targetEndpoints.LocalEndpoints;
                foreach (var item in associatedEndpoints)
                {
                    peerCryptos.TryRemove(item.ToIpEndpoint(), out _);
                }
            }
        }

        #endregion

        #region Peer Update
        public PeerInformation GetPeerInfo(Guid peerId)
        {
            PeerInfos.TryGetValue(peerId, out var val);
            return val;
        }

        protected virtual void UpdatePeerList(MessageEnvelope message)
        {
            lock (registeryLocker)
            {
                PeerList serverPeerInfo = null;
                if (message.Payload == null)
                    serverPeerInfo = new PeerList() { PeerIds = new Dictionary<Guid, PeerInfo>() };
                else
                {
                    serverPeerInfo = KnownTypeSerializer.DeserializePeerList(message.Payload, message.PayloadOffset);
                }

                List<Guid> registered = new List<Guid>();
                List<Guid> unregistered = new List<Guid>();

                foreach (var peer in Peers.Keys)
                {
                    if (!serverPeerInfo.PeerIds.ContainsKey(peer))
                    {
                        Peers.TryRemove(peer, out _);

                        if(punchedEndpoints.TryRemove(peer, out var ep) && ep !=null)
                            peerCryptos.TryRemove(ep, out _);

                        RemoveRudpModule(peer);
                        pinger.PeerUnregistered(peer);
                        PeerInfos.TryRemove(peer, out _);
                        unregistered.Add(peer);
                    }
                }

                foreach (var peer in serverPeerInfo.PeerIds.Keys)
                {
                    if (!Peers.TryGetValue(peer, out _))
                    {
                        Peers.TryAdd(peer, true);
                        CreateRudpModule(peer);
                        pinger.PeerRegistered(peer);
                        PeerInfos.TryAdd(peer, new PeerInformation(serverPeerInfo.PeerIds[peer]));
                        registered.Add(peer);
                    }
                }

               
                foreach (var peer in unregistered)
                {
                    OnPeerUnregistered?.Invoke(peer);
                }
                foreach (var peer in registered)
                {
                    OnPeerRegistered?.Invoke(peer);
                }
            }
        }

        private void RemoveRudpModule(Guid peer)
        {
            if(RUdpModules.TryRemove(peer, out var mod))
            {
                mod.Close();
            }
        }

        private void CreateRudpModule(Guid peer)
        {
            var mod = new ReliableUdpModule();
            mod.OnSend += (b, o, c) =>
            {
                SendRudpMessage(peer, b, o, c);
            };
            mod.OnReceived += HandleRUdpBytesReceived;
            RUdpModules.TryAdd(peer, mod);

        }

        private void HandleRUdpBytesReceived(byte[] arg1, int arg2, int arg3)
        {           
            var  msg = serialiser.DeserialiseEnvelopedMessage(arg1, arg2, arg3);
           
            if (UdpAwaiter.IsWaiting(msg.MessageId))
            {
                UdpAwaiter.ResponseArrived(msg);
            }
            else
                HandleUdpMessageReceived(msg);
        }
        private void SendRudpMessage(Guid toId, byte[] b, int o, int c)
        {
            MessageEnvelope message = new MessageEnvelope();
            message.Header = Constants.Rudp;
            message.SetPayload(b,o,c);
            SendUdpMesssage(toId, message);
        }

        public void SendRudpMessage(Guid to, MessageEnvelope msg)
        {
            if(RUdpModules.TryGetValue(to, out var mod))
            {
                msg.From = sessionId;
                msg.To = to;

                var stream = SharerdMemoryStreamPool.RentStreamStatic();
                serialiser.EnvelopeMessageWithBytesDontWritePayload(stream,msg);

                ArraySegment<byte> first = new ArraySegment<byte>(stream.GetBuffer(), 0, stream.Position32);
                ArraySegment<byte> second;
                if (msg.Payload == null)
                    second = new ArraySegment<byte>(new byte[0]);
                else
                    second = new ArraySegment<byte>(msg.Payload, msg.PayloadOffset, msg.PayloadCount);

                mod.Send(first,second);
                SharerdMemoryStreamPool.ReturnStreamStatic(stream);
                
            }
        }

        public void SendRudpMessage<T>(Guid to, MessageEnvelope msg, T innerMessage)
        {
            if (RUdpModules.TryGetValue(to, out var mod))
            {
                msg.From = sessionId;
                msg.To = to;

                var stream = SharerdMemoryStreamPool.RentStreamStatic();
                serialiser.EnvelopeMessageWithInnerMessage(stream, msg, innerMessage);
                mod.Send(stream.GetBuffer(), 0, stream.Position32);
                SharerdMemoryStreamPool.ReturnStreamStatic(stream);
            }
        }

        GenericMessageAwaiter<MessageEnvelope> UdpAwaiter = new GenericMessageAwaiter<MessageEnvelope>();
        public Task<MessageEnvelope> SendRudpMessageAndWaitResponse(Guid toId, MessageEnvelope message, byte[] buffer, int offset, int count, int timeoutMs = 10000)
        {
            message.SetPayload(buffer, offset, count);
            return SendRudpMessageAndWaitResponse(toId, message,timeoutMs);
        }

        public Task<MessageEnvelope> SendRudpMessageAndWaitResponse(Guid to, MessageEnvelope msg,int timeoutMs = 10000)
        {
            if (RUdpModules.TryGetValue(to, out var mod))
            {
                msg.From = sessionId;
                msg.To = to;
                msg.MessageId = Guid.NewGuid();
                var task = UdpAwaiter.RegisterWait(msg.MessageId, timeoutMs);

                var stream = SharerdMemoryStreamPool.RentStreamStatic();
                serialiser.EnvelopeMessageWithBytesDontWritePayload(stream, msg);

                ArraySegment<byte> first = new ArraySegment<byte>(stream.GetBuffer(), 0, stream.Position32);
                ArraySegment<byte> second;
                if (msg.Payload == null)
                    second = new ArraySegment<byte>(new byte[0]);
                else
                second = new ArraySegment<byte>(msg.Payload, msg.PayloadOffset, msg.PayloadCount);

                mod.Send(first, second);
                SharerdMemoryStreamPool.ReturnStreamStatic(stream);

                return task;
            }
            return Task.FromResult(new MessageEnvelope() { Header = MessageEnvelope.RequestCancelled });
        }

        public Task<MessageEnvelope> SendRudpMessageAndWaitResponse<T>(Guid to, MessageEnvelope msg, T innerMessage, int timeoutMs = 10000)
        {
            if (RUdpModules.TryGetValue(to, out var mod))
            {
                msg.From = sessionId;
                msg.To = to;
                msg.MessageId = Guid.NewGuid();
                var task = UdpAwaiter.RegisterWait(msg.MessageId, timeoutMs);

                var stream = SharerdMemoryStreamPool.RentStreamStatic();
                serialiser.EnvelopeMessageWithInnerMessage(stream, msg, innerMessage);
                mod.Send(stream.GetBuffer(), 0, stream.Position32);
                SharerdMemoryStreamPool.ReturnStreamStatic(stream);
                return task;
            }
            return Task.FromResult(new MessageEnvelope() { Header = MessageEnvelope.RequestCancelled });

        }



        #endregion

        #region Interface implementation
        void INetworkNode.SendUdpAsync(IPEndPoint ep, MessageEnvelope message, Action<PooledMemoryStream> callback, ConcurrentAesAlgorithm aesAlgorithm)
        {
            udpServer.SendAsync(ep, message, callback, aesAlgorithm);
        }

        void INetworkNode.SendUdpAsync(IPEndPoint ep, MessageEnvelope message, Action<PooledMemoryStream> callback)
        {
            udpServer.SendAsync(ep, message, callback);
        }

        void INetworkNode.SendAsyncMessage(Guid destinatioinId, MessageEnvelope message)
        {
            message.From = sessionId;
            message.To = destinatioinId;
            tcpMessageClient.SendAsyncMessage(message);
            //SendAsyncMessage(destinatioinId, message);
        }

       

     
        #endregion
    }

}
