using NetworkLibrary.Components;
using NetworkLibrary.Components.Crypto;
using NetworkLibrary.Components.Crypto.Certificate;
using NetworkLibrary.Components.Statistics;
using NetworkLibrary.MessageProtocol;
using NetworkLibrary.P2P.Components;
using NetworkLibrary.P2P.Components.HolePunch;
using NetworkLibrary.P2P.Components.Modules;
using NetworkLibrary.P2P.Components.StateManagement;
using NetworkLibrary.P2P.Components.StateManagement.Client;
using NetworkLibrary.UDP.Jumbo;
using NetworkLibrary.UDP.Reliable.Components;
using NetworkLibrary.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkLibrary.P2P.Generic
{
    class CryptoInfo
    {
        public Guid id;
        public ConcurrentAesAlgorithm algorithm;
    }
    public enum RudpChannel
    {
        Ch1 = 0,
        Ch2 = 1,
        Realtime = 2
    }
    class EndpointGroup
    {
        public IPEndPoint ToSend;
        public IPEndPoint ToReceive;
    }
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

        public PeerInformation() { }
    }
    public class ServerInfo
    {
        public IPEndPoint Endpoint;
        public string Name;
    }

    public class RelayClientBase<S> : INetworkNode
        where S : ISerializer, new()
    {
        public Action<Guid> OnPeerRegistered;
        public Action<Guid> OnPeerUnregistered;
        public Action<MessageEnvelope> OnUdpMessageReceived;
        public Action<MessageEnvelope> OnMessageReceived;
        public Action OnDisconnected;
        public RemoteCertificateValidationCallback RemoteCertificateValidationCallback;       

        public Guid SessionId => sessionId;

        public bool IsConnected { 
            get => Interlocked.CompareExchange(ref isConnected,0,0) == 1; 
            private set 
            {
                if(value)
                    Interlocked.Exchange(ref isConnected, 1);
                else
                    Interlocked.Exchange(ref isConnected, 0);
            } 
        }

        public AesMode AESMode = AesMode.GCM;

        public ConcurrentDictionary<Guid, bool> Peers = new ConcurrentDictionary<Guid, bool>();
        internal ConcurrentDictionary<Guid, PeerInformation> PeerInfos { get; private set; } = new ConcurrentDictionary<Guid, PeerInformation>();
        internal ConcurrentDictionary<Guid, List<ReliableUdpModule>> RUdpModules = new ConcurrentDictionary<Guid, List<ReliableUdpModule>>();
        internal ConcurrentDictionary<Guid, JumboModule> JumboUdpModules = new ConcurrentDictionary<Guid, JumboModule>();
        internal string connectHost;
        internal int connectPort;
        internal ClientUdpModule<S> udpServer;
        internal IPEndPoint relayServerEndpoint;
        internal SecureMessageClient<S> tcpMessageClient;

        private GenericMessageAwaiter<MessageEnvelope> Awaiter => tcpMessageClient.Awaiter;
        private ConcurrentAesAlgorithm udpEncryiptor;
        private object registeryLocker = new object();
        private ushort maxUdpPackageSize = 64200;
        private int isConnected;
        private int connecting;
        private int disposed = 0;
        private Guid sessionId;
        private PingHandler pinger = new PingHandler();
        private GenericMessageSerializer<S> serialiser = new GenericMessageSerializer<S>();
        private ConcurrentDictionary<Guid, EndpointGroup> punchedEndpoints = new ConcurrentDictionary<Guid, EndpointGroup>();
        private ConcurrentDictionary<Guid, AesTcpModule<S>> punchedTcpModules = new ConcurrentDictionary<Guid, AesTcpModule<S>>();
        internal ConcurrentDictionary<IPEndPoint, CryptoInfo> peerCryptos = new ConcurrentDictionary<IPEndPoint, CryptoInfo>();
        private ClientStateManager<S> clientStateManager;
        private X509Certificate2 clientCert;
        private int udpPort = 0;
        private bool initialised = false;
        private Stopwatch clientClock = new Stopwatch();
        double timeOffset;
        TimeSpan timeOffsetd;
        long syncCount = 0;
        public RelayClientBase(X509Certificate2 clientCert, int udpPort = 0)
        {
            if (clientCert == null)
                clientCert = CertificateGenerator.GenerateSelfSignedCertificate();
           this.clientCert = clientCert;
           this.udpPort = udpPort;
        }
        public RelayClientBase( int udpPort = 0)
        {
            this.clientCert = CertificateGenerator.GenerateSelfSignedCertificate();
            this.udpPort = udpPort;
        }
        private void Initialise()
        {
            initialised= true;
            clientStateManager = new ClientStateManager<S>(this);
            tcpMessageClient = new SecureMessageClient<S>(clientCert);
            tcpMessageClient.OnMessageReceived += HandleMessageReceived;
            tcpMessageClient.OnDisconnected += HandleDisconnect;
            tcpMessageClient.RemoteCertificateValidationCallback += CertificateValidation;

            udpServer = new ClientUdpModule<S>(udpPort, this);
            udpServer.SocketReceiveBufferSize = 12800000;
            udpServer.SocketSendBufferSize = 12800000;
            udpServer.OnBytesRecieved += HandleUdpBytesReceived;
            udpServer.StartServer();

            clientClock.Start();
        }
        public double GetTime()
        {
            return clientClock.Elapsed.TotalMilliseconds + timeOffset;
        }

        public DateTime GetDateTime()
        {
            return DateTime.UtcNow.Add(timeOffsetd);
        }

        AsyncDispatcher timesyncOperation;
        public void StartAutoTimeSync(int periodMs,bool disconnectOnTimeout,bool usePTP = false)
        {
            Interlocked.Exchange(ref timesyncOperation, new AsyncDispatcher())?.Abort();
            int failureCount = 0;
            timesyncOperation.LoopPeriodicTask(async () => 
            {
                try
                {
                    if (Interlocked.CompareExchange(ref disposed, 0, 0) == 0)
                    {
                        if (IsConnected)
                        {
                            bool result = await SyncTime(usePTP).ConfigureAwait(false);
                            if (disconnectOnTimeout && result == false )
                            {
                                if (++failureCount > 2)
                                {
                                    MiniLogger.Log(MiniLogger.LogLevel.Error, "TimeSync operation timed out ");
                                    Disconnect();
                                }
                               
                            }
                            else
                            {
                                failureCount = 0;
                            }
                        }
                        
                    }
                    else
                    {
                        timesyncOperation.Abort();
                    }
                }
                catch(Exception e) 
                {
                    MiniLogger.Log(MiniLogger.LogLevel.Error, "TimeSync operation failed with exception " + e.Message);
                    Disconnect();
                }

            }, periodMs).ConfigureAwait(false);
        }

        public void StopAutoTimeSync()
        {
            timesyncOperation?.Abort();
        }

      
        List<double> timesHistory = new List<double>();
        List<TimeSpan> timesHistoryd = new List<TimeSpan>();
        private static readonly SemaphoreSlim asyncLock = new SemaphoreSlim(1, 1);
        public async Task<bool> SyncTime(bool usePtp = false)
        {
            try
            {
                await asyncLock.WaitAsync().ConfigureAwait(false);

                if (!IsConnected)
                    return false;

                int sampleSize = 3;
                var sCnt = Interlocked.CompareExchange(ref syncCount, 0, 0);

                if (sCnt == 0)
                    sampleSize = 12;

                if (sCnt > 50)
                    sampleSize = 2;

                if (sCnt > 100)
                    sampleSize = 1;


                for (int i = 0; i < sampleSize; i++)
                {
                    var result = usePtp?await GetOffsetPTP().ConfigureAwait(false): await GetOffsetNTP().ConfigureAwait(false);
                    if (result.Succes)
                    {
                        timesHistory.Add(result.PreciseTime);
                        timesHistoryd.Add(result.DateTimeOffset);
                    }
                    else return false;
                }

                if(timesHistory.Count<4)
                    return false;

                var times = Statistics.FilterOutliers(timesHistory);
                var timesd = Statistics.FilterOutliers(timesHistoryd);
                if (timesHistory.Count > 600)
                {
                    timesHistory = timesHistory.Skip(60).ToList();
                    timesHistoryd = timesHistoryd.Skip(60).ToList();
                }

                double average = times.Sum() / times.Count();
                double averaged = timesd.Sum(ts => ts.Ticks) / timesd.Count();
                timeOffset = average;

                if (sCnt > 0)
                {
                    var calculatedAvg = TimeSpan.FromTicks((long)averaged);
                    if (timeOffsetd < calculatedAvg)
                    {
                        timeOffsetd = calculatedAvg;
                    }
                }
                else
                {
                    timeOffsetd = TimeSpan.FromTicks((long)averaged);
                }

                Interlocked.Increment(ref syncCount);
                return true;

            }
            finally 
            { 
                asyncLock.Release();
            }

        }
        class TimeResult { public double PreciseTime; public bool Succes; public DateTime ServerUTC; public TimeSpan DateTimeOffset; }
        private async Task<TimeResult> GetOffsetNTP()
        {
            var msg = new MessageEnvelope()
            {
                Header = Constants.TimeSync,
                IsInternal = true,
            };
            var now = clientClock.Elapsed.TotalMilliseconds;
            var nowd = DateTime.UtcNow;

            var response = await tcpMessageClient.SendMessageAndWaitResponse(msg, 10000).ConfigureAwait(false);
            if (response.Header != MessageEnvelope.RequestTimeout)
            {

                var serverTime = PrimitiveEncoder.ReadFixedDouble(response.Payload, response.PayloadOffset);
                var serverTimed = response.TimeStamp;

                var now1 = clientClock.Elapsed.TotalMilliseconds;
                var now1d = DateTime.UtcNow;

                var timeOffset = ((serverTime - now) + (serverTime - now1)) / 2;
                var timeOffsetd = ((serverTimed - nowd) + (serverTimed - now1d)).TotalMilliseconds / 2;
                TimeSpan offd = TimeSpan.FromMilliseconds(timeOffsetd);

                return new TimeResult() { PreciseTime = timeOffset, DateTimeOffset = offd, Succes = true };
            }
            return new TimeResult();

        }

        private async Task<TimeResult> GetOffsetPTP()
        {

            var t1 = await GetServerTime();
            if (t1.Succes)
            {
                var t2 = clientClock.Elapsed.TotalMilliseconds;
                var t2d = DateTime.UtcNow;

                var t3 = t2;
                var t3d = t2d;

                var t4 = await GetServerTime();

                if (t4.Succes)
                {
                    double offset =  ( ((t4.PreciseTime - t3) - (t2 - t1.PreciseTime)) / 2);
                    double offsetd =  ( ((t4.ServerUTC - t3d) - (t2d - t1.ServerUTC)).TotalMilliseconds / 2);
                    TimeSpan offd =TimeSpan.FromMilliseconds(offsetd);
                    return new TimeResult() { PreciseTime = offset, DateTimeOffset = offd, Succes = true };
                }
                else
                    return new TimeResult();
            }
            else
                return new TimeResult();

        }

        private async Task<TimeResult> GetServerTime()
        {
            var msg = new MessageEnvelope()
            {
                Header = Constants.TimeSync,
                IsInternal = true,
            };
            var response = await tcpMessageClient.SendMessageAndWaitResponse(msg, 10000);
            if (response.Header != MessageEnvelope.RequestTimeout)
            {
                var serverTime = PrimitiveEncoder.ReadFixedDouble(response.Payload, response.PayloadOffset);
                return new TimeResult() { PreciseTime = serverTime, ServerUTC = response.TimeStamp, Succes = true };
            }
            return new TimeResult();

        }
    public void GetTcpStatistics(out TcpStatistics stats) => tcpMessageClient.GetStatistics(out stats);

        private bool CertificateValidation(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (RemoteCertificateValidationCallback == null)
                return true;
            return RemoteCertificateValidationCallback.Invoke(sender, certificate, chain, sslPolicyErrors);
        }

        #region Connect & Disconnect
        /// <summary>
        /// Searches local network for relay server in provided port
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        public async Task<List<ServerInfo>> TryFindRelayServer(int port)
        {
            List<ServerInfo> servers = new List<ServerInfo>();
            using (var udpClient = new UdpClient())
            {
                udpClient.EnableBroadcast = true;

                //if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                //    udpClient.AllowNatTraversal(true);

                var broadcastAddress = new IPEndPoint(IPAddress.Broadcast, port);

                var request = new byte[1] { 91 };
                udpClient.Send(request, request.Length, broadcastAddress);
                bool again = false;
                do
                {
                    var receiveTask = udpClient.ReceiveAsync();

                    var result = await Task.WhenAny(receiveTask, Task.Delay(1000));
                    if (result == receiveTask)
                    {
                        var remoteEp = receiveTask.Result.RemoteEndPoint;
                       servers.Add(new ServerInfo() { Endpoint = remoteEp,
                       Name = Encoding.UTF8.GetString(receiveTask.Result.Buffer)});
                       again = true;
                    }
                    else
                    {
                        again = false;
                    }
                }
                while (again);
               
                return servers;
            }
        }

        /// <summary>
        /// Connects Async
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public async Task<bool> ConnectAsync(string host, int port)
        {
            CheckDisposedAndThrow();
            if(!initialised)
                Initialise();
            try
            {
                connectHost = host;
                connectPort = port;

                if (Interlocked.CompareExchange(ref connecting, 1, 0) == 1)
                    return false;

                if (IsConnected) return false;

                relayServerEndpoint = new IPEndPoint(IPAddress.Parse(connectHost), connectPort);

                bool result = await tcpMessageClient.ConnectAsync(host, port).ConfigureAwait(false);
                if (!result)
                {
                    throw new TimeoutException();
                }
                else
                {
                    MiniLogger.Log(MiniLogger.LogLevel.Info, "Connection established");
                }

                var stateCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                clientStateManager.CreateConnectionState()
                .Completed += (IState) =>
                {
                    if (IState.Status == StateStatus.Completed)
                    {
                        var state = IState as ClientConnectionState;
                        sessionId = state.SessionId;
                        udpEncryiptor = state.udpEncriptor;
                        tcpMessageClient.SendAsyncMessage(new MessageEnvelope()
                        {
                            IsInternal = true,
                            Header = Constants.ClientFinalizationAck,
                            MessageId = state.StateId
                        });

                        peerCryptos.TryAdd(relayServerEndpoint, new CryptoInfo() { id = Guid.Empty,algorithm = udpEncryiptor});

                        IsConnected = true;
                        pinger.PeerRegistered(sessionId);

                        timesHistory.Clear();
                        syncCount = 0;
                       
                        stateCompletion.SetResult(true);
                    }
                    else stateCompletion.TrySetException(new TimeoutException());
                };

                return await stateCompletion.Task;

            }
            catch { throw; }
            finally
            {
                Interlocked.Exchange(ref connecting, 0);
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
            CheckDisposedAndThrow();
            tcpMessageClient?.Disconnect();
           
        }

        private void HandleDisconnect()
        {
            lock (registeryLocker)
            {
                IsConnected = false;

                foreach (var peer in PeerInfos)
                {
                    OnPeerUnregistered?.Invoke(peer.Key);
                }
                PeerInfos.Clear();
                Peers.Clear();

                peerCryptos.Clear();
                ClearPunchedEndpoints();

                foreach (var item in RUdpModules)
                {
                    foreach (var m in item.Value)
                    {
                        m?.Release();
                    }
                }
                RUdpModules.Clear();
                foreach (var item in JumboUdpModules)
                {
                    item.Value?.Release();
                }
                JumboUdpModules.Clear();

                foreach (var module in punchedTcpModules)
                {
                    module.Value.Dispose();
                }
                punchedTcpModules.Clear();

                OnDisconnected?.Invoke();

            }

        }

        #endregion

        #region Ping
        CancellationTokenSource cts;
        /// <summary>
        /// Starts a ping service where this peer pings all other peers periodically.
        /// </summary>
        /// <param name="intervalMs"></param>
        /// <param name="sendToServer"></param>
        /// <param name="sendTcpToPeers"></param>
        /// <param name="sendUdpToPeers"></param>
        public void StartPingService(int intervalMs = 1000,
                                     bool sendToServer = true,
                                     bool sendTcpToPeers = true,
                                     bool sendUdpToPeers = true)
        {
            cts?.Cancel();
            cts = new CancellationTokenSource();
            try
            {
                Task.Run(() => SendPing(intervalMs, sendToServer, sendTcpToPeers, sendUdpToPeers, cts.Token));
            }
            catch(Exception e) when (e is TaskCanceledException) { };
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
                if (token.IsCancellationRequested) return;

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

                        SendUdpMessage(sessionId, msg);
                        pinger.NotifyUdpPingSent(sessionId, time);
                    }

                    time = DateTime.Now;

                    if (sendTcpToPeers)
                        BroadcastMessage(msg);
                    if (sendUdpToPeers)
                        BroadcastUdpMessage(msg);
                    foreach (var peer in Peers.Keys)
                    {
                        if (sendTcpToPeers)
                            pinger.NotifyTcpPingSent(peer, time);
                        if (sendUdpToPeers)
                        {
                            //SendUdpMesssage(peer, msg);
                            pinger.NotifyUdpPingSent(peer, time);
                        }

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
                    if(TryGetAesTcpModule(message.From,out var module))
                    {
                        message.From = sessionId;

                        module.SendAsync(message);
                    }
                    else
                    {
                        message.From = sessionId;
                        tcpMessageClient.SendAsyncMessage(message);
                    }


                }
                else
                    SendUdpMessage(message.From, message);
            }


        }

        private void HandlePong(MessageEnvelope message, bool isTcp = true)
        {
            if (isTcp) pinger.HandleTcpPongMessage(message);
            else pinger.HandleUdpPongMessage(message);
        }
        /// <summary>
        /// Gets Tcp Ping status per peer
        /// </summary>
        /// <returns>Dictionary of peer id and latency</returns>
        public Dictionary<Guid, double> GetTcpPingStatus()
        {
            return pinger.GetTcpLatencies();
        }

        /// <summary>
        /// Gets Udp Ping status per peer
        /// </summary>
        /// <returns>Dictionary of peer id and latency</returns>
        public Dictionary<Guid, double> GetUdpPingStatus()
        {
            return pinger.GetUdpLatencies();
        }

        #endregion Ping

        #region Wrap
        private void GetUdpSendParams(Guid toId, out ConcurrentAesAlgorithm algo, out IPEndPoint endpoint)
        {
            if (TryGetTraversedEndpoint(toId, out var endpoints))
            {
                endpoint = endpoints.ToSend;
                algo = peerCryptos[endpoint].algorithm;
            }
            else
            {
                endpoint = relayServerEndpoint;
                algo = udpEncryiptor;

            }
        }

        private bool IsUdpDirectConnectionAvailable(Guid clientId)
        {
            return punchedEndpoints.ContainsKey(clientId);
        }

        private bool TryGetTraversedEndpoint(Guid target, out EndpointGroup ep)
        {
            return punchedEndpoints.TryGetValue(target, out ep);
        }

        private bool AddPunchedEndpoint(Guid clientId,EndpointGroup endpoints)
        {
            return punchedEndpoints.TryAdd(clientId, endpoints);
        }

        private bool RemovePunchedEndpoint(Guid clientId, out EndpointGroup endpoints)
        {
            return punchedEndpoints.TryRemove(clientId, out endpoints);
        }

        private void ClearPunchedEndpoints()
        {
            punchedEndpoints.Clear();
        }

        private bool TryGetAesTcpModule(Guid clientId, out AesTcpModule<S> module)
        {
            return punchedTcpModules.TryGetValue(clientId, out module);
        }

        private bool AddPunchedTcpModule(Guid clientId, AesTcpModule<S> tcpModule)
        {
            return punchedTcpModules.TryAdd(clientId, tcpModule);
        }

        private bool RemovePunchedTcpModule(Guid clientId, out AesTcpModule<S> tcpModule)
        {
            return punchedTcpModules.TryRemove(clientId, out tcpModule);
        }

        private void ClearPunchedTcpModules()
        {
            punchedTcpModules.Clear();
        }

        private bool TryGetRudpModules(Guid clientId, out List<ReliableUdpModule> mod)
        {
            return RUdpModules.TryGetValue(clientId, out mod);
        }

        private bool TryAddRUdpModules(Guid clientId, List<ReliableUdpModule> mod)
        {
            return RUdpModules.TryAdd(clientId, mod);
        }

        private void TryGetJumboUdpModule()
        {

        }
        #endregion

        #region Send

        private void SendUdpMesssageInternal(Guid toId, MessageEnvelope message)
        {
            GetUdpSendParams(toId, out ConcurrentAesAlgorithm algo, out IPEndPoint endpoint);

            if (message.PayloadCount > maxUdpPackageSize)
            {
                SendLargeUdpMessage(toId,message);
                return;
            }

            if (!udpServer.TrySendAsync(endpoint, message, algo, out var excessStream))
            {

                if (JumboUdpModules.TryGetValue(toId, out var mod))
                    mod.Send(excessStream.GetBuffer(), 0, excessStream.Position32);
                else
                    MiniLogger.Log(MiniLogger.LogLevel.Error, "Unable To find jumbo module with Id: " + toId + " in session " + sessionId);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void SendLargeUdpMessage(Guid toId, MessageEnvelope message)
        {
            // you cant store buffer outside the scope..
            if (message.KeyValuePairs != null)
            {
                var buffer = stackalloc byte[65000];
                int offset = 0;
                serialiser.EnvelopeMessageWithBytesDontWritePayload(buffer, ref offset, message, message.PayloadCount);
                var s1 = new SegmentUnsafe(buffer, 0, offset);
                var s2 = new Segment(message.Payload, message.PayloadOffset, message.PayloadCount);

                if (JumboUdpModules.TryGetValue(toId, out var mod))
                    mod.Send(in s1, in s2);
            }
            else
            {
                var buffer = stackalloc byte[256+message.Header.Length*4];
                int offset = 0;
                serialiser.EnvelopeMessageWithBytesDontWritePayload(buffer, ref offset, message, message.PayloadCount);
                var s1 = new SegmentUnsafe(buffer, 0, offset);
                var s2 = new Segment(message.Payload, message.PayloadOffset, message.PayloadCount);

                if (JumboUdpModules.TryGetValue(toId, out var mod))
                    mod.Send(in s1, in s2);
            }
        }

        private void SendUdpMesssageInternal<T>(Guid toId, MessageEnvelope message, T innerMessage)
        {
            GetUdpSendParams(toId, out ConcurrentAesAlgorithm algo, out IPEndPoint endpoint);

            if (!udpServer.TrySendAsync(endpoint, message, innerMessage, algo, out var excessStream))
            {
                if (JumboUdpModules.TryGetValue(toId, out var mod))
                    mod.Send(excessStream.GetBuffer(), 0, excessStream.Position32);
                else
                    MiniLogger.Log(MiniLogger.LogLevel.Error, "Unable To find jumbo module with Id: " + toId + " in session " + sessionId);

            }

        }

        private void SendUdpMesssageInternal(Guid toId, MessageEnvelope message, Action<PooledMemoryStream> serializationCallback)
        {
           
            GetUdpSendParams(toId, out ConcurrentAesAlgorithm algo, out IPEndPoint endpoint);

            if (!udpServer.TrySendAsync(endpoint, message, serializationCallback, algo, out var excessStream, false))
            {
                if (JumboUdpModules.TryGetValue(toId, out var mod))
                    mod.Send(excessStream.GetBuffer(), 0, excessStream.Position32);
                else
                    MiniLogger.Log(MiniLogger.LogLevel.Error, "Unable To find jumbo module with Id: " + toId + " in session " + sessionId);

            }

        }

        /// <summary>
        /// Sends the UDP message with bytes provided in envelope payload.
        /// </summary>
        /// <param name="toId">To PeerId.</param>
        /// <param name="message">The message envelope.</param>
        public void SendUdpMessage(Guid toId, MessageEnvelope message)
        {
            if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
                return;
            message.From = sessionId;
            message.To = toId;

            SendUdpMesssageInternal(toId, message);

        }

        /// <summary>
        /// Sends the UDP message with callback. Right after envelope bytes are serialized,
        /// Callback brings serialization stream for custom payload, this is to avoid extra copy.
        /// </summary>
        /// <param name="toId">To PeerId.</param>
        /// <param name="message">The message envelope.</param>
        /// <param name="serializationCallback">The serialization callback.</param>
        public void SendUdpMessage(Guid toId, MessageEnvelope message, Action<PooledMemoryStream> serializationCallback)
        {
            if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
                return;
            message.From = sessionId;
            message.To = toId;

            SendUdpMesssageInternal(toId, message,serializationCallback);

        }

        /// <summary>
        /// Sends the UDP message.
        /// </summary>
        /// <typeparam name="T">The secondary message to be serialized</typeparam>
        /// <param name="toId">To PeerId.</param>
        /// <param name="message">The message envelope.</param>
        /// <param name="innerMessage">The inner message.</param>
        public void SendUdpMessage<T>(Guid toId, MessageEnvelope message, T innerMessage)
        {
            if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
                return;

            message.From = sessionId;
            message.To = toId;


            SendUdpMesssageInternal(toId, message, innerMessage);
        }


        #region Broadcast/Multicast Udp       
        
       

        /// <summary>
        /// Broadcasts the UDP message to all connected peers.
        /// </summary>
        /// <param name="message">The message.</param>
        public void BroadcastUdpMessage(MessageEnvelope message)
        {
            if (Peers.Count > 0)
            {
                message.From = sessionId;
                bool sendToRelay = false;
                foreach (var item in Peers)
                {
                    if (IsUdpDirectConnectionAvailable(item.Key))
                    {
                        message.To = item.Key;
                        SendUdpMesssageInternal(item.Key, message);
                    }
                    else
                    {
                        sendToRelay = true;
                    }

                }
                if (sendToRelay)
                {
                    message.To = Guid.Empty;
                    if (!udpServer.TrySendAsync(relayServerEndpoint, message, udpEncryiptor, out _))
                    {
                        // unicast, message is too large.
                        foreach (var item in Peers)
                        {
                            if (!IsUdpDirectConnectionAvailable(item.Key))
                            {
                                message.To = item.Key;
                                SendUdpMesssageInternal(item.Key, message);
                            }
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Broadcasts the UDP message to all connected peers.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="message">The message.</param>
        /// <param name="innerMessage">The inner message.</param>
        public void BroadcastUdpMessage<T>(MessageEnvelope message, T innerMessage)
        {
            if (Peers.Count > 0)
            {
                message.From = sessionId;
                bool sendToRelay = false;
                foreach (var item in Peers)
                {
                    if (IsUdpDirectConnectionAvailable(item.Key))
                    {
                        message.To = item.Key;
                        SendUdpMesssageInternal(item.Key, message, innerMessage);
                    }
                    else
                    {
                        sendToRelay = true;
                    }

                }
                if (sendToRelay)
                {
                    message.To = Guid.Empty;
                    if (!udpServer.TrySendAsync(relayServerEndpoint, message, innerMessage, udpEncryiptor, out _))
                    {
                        // unicast, message is too large.
                        foreach (var item in Peers)
                        {
                            if (!IsUdpDirectConnectionAvailable(item.Key))
                            {
                                message.To = item.Key;
                                SendUdpMesssageInternal(item.Key, message, innerMessage);
                            }
                        }
                    }
                }
            }

        }
        internal void MulticastUdpMessage(MessageEnvelope message, ICollection<Guid> targets)
        {
           
            if (Peers.Count < 0)
                return;
            message.From = sessionId;
            bool sendToRelay = false;
            foreach (var target in targets)
            {
                if (TryGetTraversedEndpoint(target, out var ep))
                {
                    message.To = target;
                    SendUdpMesssageInternal(target, message);
                }
                else
                {
                    sendToRelay = true;
                }
            }
            if (sendToRelay)
            {
                message.To = Guid.Empty;
                if (!udpServer.TrySendAsync(relayServerEndpoint, message, udpEncryiptor, out _))  
                {
                    // unicast if too large
                    foreach (var target in targets)
                    {
                        if (target == sessionId)
                            continue;
                        if (!IsUdpDirectConnectionAvailable(target))
                        {
                            message.To = target;
                            SendUdpMesssageInternal(target, message);
                        }
                    }
                }
            }

        }

        internal void MulticastUdpMessage<T>(MessageEnvelope message, ICollection<Guid> targets, T innerMessage)
        {
            if (Peers.Count < 0)
                return;
            message.From = sessionId;
            bool sendToRelay = false;
            foreach (var target in targets)
            {
                if (TryGetTraversedEndpoint(target, out var ep))
                {
                    message.To = target;
                    SendUdpMesssageInternal(target, message,innerMessage);
                }
                else
                {
                    sendToRelay = true;
                }
            }
            if (sendToRelay)
            {
                message.To = Guid.Empty;
                if (!udpServer.TrySendAsync(relayServerEndpoint, message, innerMessage, udpEncryiptor, out _))
                {
                    foreach (var target in targets)
                    {
                        if (!IsUdpDirectConnectionAvailable(target))
                        {
                            if (target == sessionId)
                                continue;

                            message.To = target;
                            SendUdpMesssageInternal(target, message,innerMessage);
                        }
                    }
                }
            }
        }

        #endregion

        /// <summary>
        /// Broadcasts Tcp message to all connected peers.
        /// </summary>
        /// <param name="message">The message.</param>
        public void BroadcastMessage(MessageEnvelope message)
        {
            if (Peers.Count > 0)
            {
                message.From = sessionId;
                //message.To = Guid.Empty;
                foreach (var peer in Peers)
                {
                    if (TryGetAesTcpModule(peer.Key, out var module))
                    {
                       // message.To = peer.Key;
                        module.SendAsync(message);
                    }
                    else
                    {
                        message.To = peer.Key;
                        tcpMessageClient.SendAsyncMessage(message);
                    }
                }
              
            }
        }

        /// <summary>
        /// Broadcasts Tcp message.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="message">The message.</param>
        /// <param name="innerMessage">The inner message.</param>
        public void BroadcastMessage<T>(MessageEnvelope message, T innerMessage)
        {
            if (Peers.Count > 0)
            {
                message.From = sessionId;
                message.To = Guid.Empty;
                tcpMessageClient.SendAsyncMessage(message, innerMessage);
            }
        }

        /// <summary>
        /// Sends the asynchronous TCP message.
        /// </summary>
        /// <param name="toId">To PeerId.</param>
        /// <param name="message">The message envelope.</param>
        public void SendAsyncMessage(Guid toId, MessageEnvelope message)
        {
            if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
                return;

            message.From = sessionId;
            message.To = toId;
            if(TryGetAesTcpModule(toId, out var module))
            {
                module.SendAsync(message);
            }
            else
                tcpMessageClient.SendAsyncMessage(message);
        }

        /// <summary>
        /// Sends the asynchronous TCP message.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="toId">To PeerId.</param>
        /// <param name="envelope">The message envelope.</param>
        /// <param name="message">The message.</param>
        public void SendAsyncMessage<T>(Guid toId, MessageEnvelope envelope, T message)
        {
            if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
                return;

            envelope.From = sessionId;
            envelope.To = toId;
            if (TryGetAesTcpModule(toId, out var module))
            {
                module.SendAsync(envelope, message);
            }
            else
                tcpMessageClient.SendAsyncMessage(envelope, message);
        }

        /// <summary>
        /// Sends the asynchronous message.
        /// </summary>
        /// <param name="toId">To PeerId.</param>
        /// <param name="envelope">The message envelope.</param>
        /// <param name="serializationCallback">The serialization callback.</param>
        public void SendAsyncMessage(Guid toId, MessageEnvelope envelope, Action<PooledMemoryStream> serializationCallback)
        {
            if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
                return;

            envelope.From = sessionId;
            envelope.To = toId;
            if (TryGetAesTcpModule(toId, out var module))
            {
                module.SendAsync(envelope,serializationCallback);
            }
            else
                tcpMessageClient.SendAsyncMessage(envelope, serializationCallback);
        }

        /// <summary>
        /// Sends the asynchronous TCP message.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="toId">To identifier.</param>
        /// <param name="message">The message envelope.</param>
        /// <param name="messageHeader">The message header.</param>
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
            if (TryGetAesTcpModule(toId, out var module))
            {
                module.SendAsync(envelope,message);
            }
            else
                tcpMessageClient.SendAsyncMessage(envelope, message);
        }

        /// <summary>
        /// Sends a TCP message and wait response with a timeout.
        /// Receiving end must send reply with same MessageId.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="toId">To identifier.</param>
        /// <param name="message">The message.</param>
        /// <param name="messageHeader">The message header.</param>
        /// <param name="timeoutMs">The timeout ms.</param>
        /// <returns></returns>
        public Task<MessageEnvelope> SendRequestAndWaitResponse<T>(Guid toId, T message, string messageHeader, int timeoutMs = 10000)
        {
            var envelope = new MessageEnvelope()
            {
                From = sessionId,
                To = toId,
                MessageId = Guid.NewGuid(),
                Header = messageHeader == null ? typeof(T).Name : messageHeader
            };

            Task<MessageEnvelope> task;
            if (TryGetAesTcpModule(toId, out var module))
            {
                task = module.SendMessageAndWaitResponse(envelope, message,timeoutMs);
            }
            else
                task = tcpMessageClient.SendMessageAndWaitResponse(envelope, message, timeoutMs);
            return task;
        }

        /// <summary>
        /// Sends a TCP message and wait response with a timeout.
        /// Receiving end must send reply with same MessageId.
        /// </summary>
        /// <param name="toId">To identifier.</param>
        /// <param name="message">The message.</param>
        /// <param name="timeoutMs">The timeout ms.</param>
        /// <returns></returns>
        public Task<MessageEnvelope> SendRequestAndWaitResponse(Guid toId, MessageEnvelope message, int timeoutMs = 10000)
        {
            message.From = sessionId;
            message.To = toId;

            Task<MessageEnvelope> task;
            if (TryGetAesTcpModule(toId, out var module))
            {
                task = module.SendMessageAndWaitResponse(message, timeoutMs);
            }
            else
                task = tcpMessageClient.SendMessageAndWaitResponse(message, timeoutMs);
            return task;
        }

        /// <summary>
        /// Sends a TCP message and wait response with a timeout.
        /// Receiving end must send reply with same MessageId.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="toId">To identifier.</param>
        /// <param name="envelope">The envelope.</param>
        /// <param name="message">The message.</param>
        /// <param name="timeoutMs">The timeout ms.</param>
        /// <returns></returns>
        public Task<MessageEnvelope> SendRequestAndWaitResponse<T>(Guid toId, MessageEnvelope envelope, T message, int timeoutMs = 10000)
        {
            envelope.From = sessionId;
            envelope.To = toId;
            Task<MessageEnvelope> task;
            if (TryGetAesTcpModule(toId, out var module))
            {
                task = module.SendMessageAndWaitResponse(envelope, message, timeoutMs);
            }
            else
                task = tcpMessageClient.SendMessageAndWaitResponse(envelope, message, timeoutMs);
            return task;
        }

        #endregion

        #region Receive

        [ThreadStatic]
        private static byte[] udpReceiveBuffer;
        private static byte[] GetBuffer()
        {
            if (udpReceiveBuffer == null)
            {
                udpReceiveBuffer = ByteCopy.GetNewArray(65000, true);
            }
            return udpReceiveBuffer;
        }

        private void HandleUdpBytesReceived(IPEndPoint adress, byte[] bytes, int offset, int count)
        {
            if (peerCryptos.TryGetValue(adress, out var crypto))
            {
                byte[] decryptBuffer = null;

                try
                {
                    MessageEnvelope msg;
                    if (crypto != null)
                    {
                        decryptBuffer = GetBuffer();
                        int amountDecrypted = crypto.algorithm.DecryptInto(bytes, offset, count, decryptBuffer, 0);
                        ParseMessage(decryptBuffer, 0, amountDecrypted);
                    }
                    else
                    {
                        ParseMessage(bytes, offset, count);
                    }

                    void ParseMessage(byte[] decryptedBytes, int byteOffset, int byteCount)
                    {
                        msg = serialiser.DeserialiseEnvelopedMessage(decryptedBytes, byteOffset, byteCount);
                        if(msg.To == Guid.Empty)
                            msg.To = sessionId;
                        if(adress != relayServerEndpoint && msg.From == Guid.Empty)
                        {
                            if (crypto == null)
                            {
                                MiniLogger.Log(MiniLogger.LogLevel.Error, "Unable to find decrptor from " + adress);
                                return;
                            }
                              
                            msg.From = crypto.id;
                        }
                            
                        if (!clientStateManager.HandleMessage(adress, msg))
                        {
                            HandleUdpMessageReceived(msg);
                        }

                    }

                }
                catch (Exception e)
                {
                    var b = decryptBuffer;
                    MiniLogger.Log(MiniLogger.LogLevel.Error, "Relay Client Failed to deserialise envelope message " + e.Message +"\n"+ e.StackTrace);

                }
            }
            

        }
        private void HandleUdpMessageReceived(MessageEnvelope message)
        {
            if (message.Header == null) return;
            
            switch (message.Header)
            {
                case Constants.Ping:
                    HandlePing(message, isTcp: false);
                    return;
                case Constants.Pong:
                    HandlePong(message, isTcp: false);
                    break;
                    // jumbo udp
                case Constants.Judp:
                   
                    if (JumboUdpModules.TryGetValue(message.From, out var m))
                    {
                        m.HandleReceivedSegment(message.Payload, message.PayloadOffset, message.PayloadCount);
                    }
                    break;
                    // reliable udp
                case Constants.Rudp:
                    if (TryGetRudpModules(message.From, out var mod))
                    {
                        mod[0].HandleBytes(message.Payload, message.PayloadOffset, message.PayloadCount);
                    }
                    break;
                case Constants.Rudp1:
                    if (TryGetRudpModules(message.From, out var mod2))
                    {
                        mod2[1].HandleBytes(message.Payload, message.PayloadOffset, message.PayloadCount);
                    }
                    break;
                case Constants.Rudp2:
                    if (TryGetRudpModules(message.From, out var mod3))
                    {
                        mod3[2].HandleBytes(message.Payload, message.PayloadOffset, message.PayloadCount);
                    }
                    break;
                default:
                    HandleUdpMessage(message);
                    break;
            };

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
                else if (message.Header == Constants.NotifyPeerListUpdate)
                    UpdatePeerList(message);
                else if (message.Header == "CmdTcpHp")
                    InitiateRemoteTcpHolepunch(message);
                else
                    HandleMessage(message);
            }
            else
            {
                if (Awaiter.IsWaiting(message.MessageId))
                {
                    Awaiter.ResponseArrived(message);
                    return;
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
        /// <summary>
        /// Requests Udp hole punch between this and target peer.
        /// </summary>
        /// <param name="peerId">The peer target.</param>
        /// <param name="timeOut">The time out.</param>
        /// <param name="encrypted">if set to <c>true</c> [encrypted].</param>
        /// <returns></returns>
        public bool RequestHolePunch(Guid peerId, int timeOut = 10000, bool encrypted = true)
        {
            return RequestHolePunchAsync(peerId, timeOut, encrypted).Result;
        }

        /// <summary>
        /// Requests Udp hole punch between this and target peer.
        /// </summary>
        /// <param name="peerId">The peer identifier.</param>
        /// <param name="timeOut">The time out.</param>
        /// <param name="encrypted">if set to <c>true</c> [encrypted].</param>
        /// <returns></returns>
        public Task<bool> RequestHolePunchAsync(Guid peerId, int timeOut, bool encrypted = true)
        {
            if (clientStateManager.IsHolepunchStatePending(peerId) )
            {
                return Task.FromResult(false);
            }
            if (IsUdpDirectConnectionAvailable(peerId))
            {
                return Task.FromResult(true);
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

        public Task<bool> RequestTcpHolePunchAsync(Guid destinationId)
        {
            return RequestTcpHpAsync(destinationId, true);
        }
        private Task<bool> RequestTcpHpAsync(Guid destinationId, bool userRequest)
        {
            if(clientStateManager.IsTCPHolepunchStatePending(destinationId))
            { return Task.FromResult(false); }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var state = clientStateManager.CreateTcpHolePunchState(destinationId);
            state.Completed += async (s) =>
            {
                try
                {
                    if (state.Status == StateStatus.Completed)
                    {
                        tcs.SetResult(true);
                        RegisterTcpNode(state);
                    }
                    else
                    {
                        if (userRequest)
                        {
                            // here we do reverse request
                            MiniLogger.Log(MiniLogger.LogLevel.Debug, "Starting Reverse TCP HP");
                            MessageEnvelope msg = new MessageEnvelope();
                            msg.Header = "CmdTcpHp";
                            msg.IsInternal = true;
                            var response = await SendRequestAndWaitResponse(destinationId, msg, 10000);
                            if (response.Header == "SuccessHpRemote")
                                tcs.SetResult(true);
                            else
                                tcs.SetResult(false);
                        }
                        else
                            tcs.SetResult(false); 
                       

                    }
                }
                catch (Exception e)
                {
                    MiniLogger.Log(MiniLogger.LogLevel.Error, "Error on Tcp HP" + e.Message);
                    tcs.SetResult(false);
                }

            };
            return tcs.Task;
        }
        // Callback from holepunch state.
        // This associates a crypto algorithm on an enpoint so we can decirpt the messages
        internal void RegisterCrypto(byte[] key, List<EndpointData> associatedEndpoints, Guid associatedClientId)
        {
            ConcurrentAesAlgorithm crypto = null;
            if (key != null)
                crypto = new ConcurrentAesAlgorithm(key,AESMode);
            foreach (var item in associatedEndpoints)
            {
                peerCryptos.TryAdd(item.ToIpEndpoint(), new CryptoInfo() { id = associatedClientId, algorithm = crypto });
            }
        }

        // This is called on succesfull completion of a holepucnh
        internal void HandleHolepunchSuccess(ClientHolepunchState state)
        {
            foreach (var ep in state.targetEndpoints.LocalEndpoints)
            {
                var ipEp = ep.ToIpEndpoint();
                if (ipEp.Equals(state.succesfulEpToReceive) || ipEp.Equals(state.succesfullEpToSend))
                {
                    continue;
                }
                peerCryptos.TryRemove(ipEp, out _);
            }
            if (!peerCryptos.ContainsKey(state.succesfulEpToReceive))
            {
                MiniLogger.Log(MiniLogger.LogLevel.Error, "Error receive endpoint is missing on cryptos");
                peerCryptos.TryAdd(state.succesfulEpToReceive, new CryptoInfo()
                {
                    id = state.destinationId,
                    algorithm = new ConcurrentAesAlgorithm(state.cryptoKey, AESMode)
                });
            }
            if (!peerCryptos.ContainsKey(state.succesfullEpToSend))
            {
                MiniLogger.Log(MiniLogger.LogLevel.Error, "Error send endpoint is missing on cryptos");
                peerCryptos.TryAdd(state.succesfullEpToSend, new CryptoInfo()
                {
                    id = state.destinationId,
                    algorithm = new ConcurrentAesAlgorithm(state.cryptoKey, AESMode)
                });
            }

            AddPunchedEndpoint(state.destinationId, new EndpointGroup() { ToReceive = state.succesfulEpToReceive,ToSend = state.succesfullEpToSend });

            MiniLogger.Log(MiniLogger.LogLevel.Info, $"HolePunched, Receive Endpoint: {state.succesfulEpToReceive}, Send Endpoint {state.succesfullEpToSend}");
        }


        internal void HandleHolepunchFailure(ClientHolepunchState state)
        {
            if (state.targetEndpoints != null && state.targetEndpoints.LocalEndpoints != null)
            {
                var associatedEndpoints = state.targetEndpoints.LocalEndpoints;
                foreach (var item in associatedEndpoints)
                {
                    peerCryptos.TryRemove(item.ToIpEndpoint(), out _);
                }
            }
        }
        private async void InitiateRemoteTcpHolepunch(MessageEnvelope message)
        {
            MessageEnvelope msg = new MessageEnvelope();
            msg.MessageId = message.MessageId;
            msg.Header = "FailHpRemote";

            try
            {
                bool result = await RequestTcpHpAsync(message.From,false);
               
                if (result)
                {
                    msg.Header = "SuccessHpRemote";
                }
                SendAsyncMessage(message.From, msg);
            }
            catch(Exception e)
            {
                MiniLogger.Log(MiniLogger.LogLevel.Error, "Error occured on remote hp request" + e.Message);
                SendAsyncMessage(message.From, msg);
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
                        HandleUnRegistered(peer);
                        unregistered.Add(peer);
                    }
                }

                foreach (var peer in serverPeerInfo.PeerIds.Keys)
                {
                    if (!Peers.TryGetValue(peer, out _))
                    {
                        HandleRegistered(peer, serverPeerInfo.PeerIds);
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

        protected internal void HandleRegistered(Guid peerId, Dictionary<Guid, PeerInfo> peerIds)
        {
            Peers.TryAdd(peerId, true);
            CreateRudpModule(peerId);
            CreateJudpModule(peerId);
            pinger.PeerRegistered(peerId);
            PeerInfos.TryAdd(peerId, new PeerInformation(peerIds[peerId]));
        }


        protected internal void HandleUnRegistered(Guid peerId)
        {
            Peers.TryRemove(peerId, out _);

            if (RemovePunchedEndpoint(peerId, out var ep) && ep != null)
            {
                peerCryptos.TryRemove(ep.ToReceive, out _);
                peerCryptos.TryRemove(ep.ToSend, out _);

            }
            if(RemovePunchedTcpModule(peerId,out var module))
            {
                module.Dispose();
            }
            RemoveRudpModule(peerId);
            RemoveJudpModule(peerId);
            pinger.PeerUnregistered(peerId);
            PeerInfos.TryRemove(peerId, out _);
           
           
        }
        #endregion

        #region Jumbo
        private void RemoveJudpModule(Guid peerId)
        {
            if(JumboUdpModules.TryRemove(peerId, out var mod))
                mod.Release();
        }

        private void CreateJudpModule(Guid peerId)
        {
            if(JumboUdpModules.TryRemove(peerId, out var m))
            {
                m.Release();
            }

            var mod = new JumboModule();
            mod.SendToSocket = (b, o, c) => FrameJumboMessageChunk(peerId, b, o, c);
            mod.MessageReceived = HandleJumboMessage;
            JumboUdpModules.TryAdd(peerId, mod);
        }
        private void FrameJumboMessageChunk(Guid toId, byte[] b, int o, int c)
        {
            MessageEnvelope message = new MessageEnvelope();
            message.Header = Constants.Judp;
            message.SetPayload(b, o, c);

            SendUdpRaw(toId, message, b, o, c);
        }

        private void HandleJumboMessage(byte[] arg1, int arg2, int arg3)
        {
            var msg = serialiser.DeserialiseEnvelopedMessage(arg1, arg2, arg3);
            HandleUdpMessageReceived(msg);
        }

        [ThreadStatic]
        private static byte[] buffer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte[] GetBuffer2()
        {
            if (buffer == null)
            {
                buffer = ByteCopy.GetNewArray(65000, true);
            }
            return buffer;
        }

        private void SendUdpRaw(Guid toId, MessageEnvelope message, byte[] b, int o, int c)
        {
            if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
                return;
            int offset = 0;
            if (IsUdpDirectConnectionAvailable(toId))
            {
                message.To = Guid.Empty;
                message.From = Guid.Empty;
                offset = 32;
                c = c - 32;
                message.SetPayload(b, o, c);
            }
            else
            {
                message.To = toId;
                message.From = sessionId;
                offset = 0;
            }
            
           // WriteRudpRouterHeaderIfNeeded(message, toId);

            o = offset;
            serialiser.EnvelopeMessageWithBytesDontWritePayload(b, ref o, message, message.PayloadCount);
            o = offset;

            ConcurrentAesAlgorithm algo;
            IPEndPoint endpoint;
            if (TryGetTraversedEndpoint(toId, out var endpoints))
            {
                endpoint = endpoints.ToSend;
                algo = peerCryptos[endpoint].algorithm;

            }
            else
            {
                endpoint = relayServerEndpoint;
                algo = udpEncryiptor;

            }
            var buff = GetBuffer2();
            int amountEncypted = algo.EncryptInto(b, offset, c, buff, 0);
            udpServer.SendBytesToClient(endpoint, buff, 0, amountEncypted);

        }

        #endregion

        #region Rudp
        private void RemoveRudpModule(Guid peer)
        {
            try
            {
                if (RUdpModules.TryRemove(peer, out var mod))
                {
                    foreach (var item in mod)
                    {
                        item.Release();
                    }
                }
            }
            catch { }
           
        }

        private void CreateRudpModule(Guid peer)
        {
            //ch1
            var mod = new ReliableUdpModule();
            mod.OnSend += (b, o, c) =>
            {
                FrameRudpMessage(peer,1, b, o, c);
            };
            mod.OnReceived += (b,o,c)=>HandleRUdpBytesReceived(peer,b,o,c);

            //ch2
            var mod1 = new ReliableUdpModule();
            mod1.OnSend += (b, o, c) =>
            {
                FrameRudpMessage(peer,2, b, o, c);
            };
            mod1.OnReceived += (b, o, c) => HandleRUdpBytesReceived(peer, b, o, c);

            //ch3
            var mod2 = new ReliableUdpModule();
            mod2.Configure(ReliableUdpModule.ConfigType.Realtime);
            mod2.OnSend += (b, o, c) =>
            {
                FrameRudpMessage(peer,3, b, o, c);
            };
            mod2.OnReceived += (b, o, c) => HandleRUdpBytesReceived(peer, b, o, c);

            List<ReliableUdpModule> l = new List<ReliableUdpModule>
            {
                mod,
                mod1,
                mod2
            };
            TryAddRUdpModules(peer, l);

        }

        private void HandleRUdpBytesReceived(Guid associatedPeerId, byte[] arg1, int arg2, int arg3)
        {
            var msg = serialiser.DeserialiseEnvelopedMessage(arg1, arg2, arg3);
            msg.From = associatedPeerId;
            msg.To = sessionId;
            if (Awaiter.IsWaiting(msg.MessageId))
            {
                Awaiter.ResponseArrived(msg);
            }
            else
                HandleUdpMessageReceived(msg);
        }

        private void FrameRudpMessage(Guid toId,int ch, byte[] b, int o, int c)
        {
            MessageEnvelope message = new MessageEnvelope();
            switch (ch)
            {
                case 1:
                    message.Header = Constants.Rudp;
                    break;
                case 2:
                    message.Header = Constants.Rudp1;
                    break;
                case 3:
                    message.Header = Constants.Rudp2;
                    break;
            }
            message.SetPayload(b, o, c);

            SendUdpRaw(toId, message, b, o, c);
        }

       

        /// <summary>
        /// Sends Reliable Udp message.
        /// </summary>
        /// <param name="to">To.</param>
        /// <param name="msg">The Message.</param>
        /// <param name="channel">The channel.</param>
        public void SendRudpMessage(Guid to, MessageEnvelope msg, RudpChannel channel = RudpChannel.Ch1)
        {
            if (TryGetRudpModules(to, out var mod))
            {
                //WriteRudpRouterHeaderIfNeeded(msg, to);
                msg = MessageEnvelope.CloneWithNoRouter(msg);

                var stream = SharerdMemoryStreamPool.RentStreamStatic();//ClientUdpModule.GetTLSStream();

                serialiser.EnvelopeMessageWithBytesDontWritePayload(stream, msg, msg.PayloadCount);

                var first = new Segment(stream.GetBuffer(), 0, stream.Position32);
                Segment second;

                if (msg.Payload == null)
                    second = new Segment(new byte[0], 0, 0);
                else
                    second = new Segment(msg.Payload, msg.PayloadOffset, msg.PayloadCount);

                mod[(int)channel].Send(first, second);
                SharerdMemoryStreamPool.ReturnStreamStatic(stream);
            }
        }

        /// <summary>
        /// Sends Reliable Udp message.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="to">To.</param>
        /// <param name="msg">The MSG.</param>
        /// <param name="innerMessage">The inner message.</param>
        /// <param name="channel">The channel.</param>
        public void SendRudpMessage<T>(Guid to, MessageEnvelope msg, T innerMessage, RudpChannel channel = RudpChannel.Ch1)
        {
            if (TryGetRudpModules(to, out var mod))
            {
                //WriteRudpRouterHeaderIfNeeded(msg, to);
                msg = MessageEnvelope.CloneWithNoRouter(msg);

                var stream = SharerdMemoryStreamPool.RentStreamStatic();//ClientUdpModule.GetTLSStream();
                serialiser.EnvelopeMessageWithInnerMessage(stream, msg, innerMessage);
                mod[(int)channel].Send(stream.GetBuffer(), 0, stream.Position32);

                SharerdMemoryStreamPool.ReturnStreamStatic(stream);
            }
        }

        /// <summary>
        /// Sends the reliable message and wait response.
        /// Receiving end must reply with same message id.
        /// </summary>
        /// <param name="to">To.</param>
        /// <param name="msg">The MSG.</param>
        /// <param name="timeoutMs">The timeout ms.</param>
        /// <param name="channel">The channel.</param>
        /// <returns></returns>
        public Task<MessageEnvelope> SendRudpMessageAndWaitResponse(Guid to, MessageEnvelope msg, int timeoutMs = 10000, RudpChannel channel = RudpChannel.Ch1)
        {
            if (TryGetRudpModules(to, out var mod))
            {
                //WriteRudpRouterHeaderIfNeeded(msg, to);
                msg = MessageEnvelope.CloneWithNoRouter(msg);

                msg.MessageId = Guid.NewGuid();
                var task = Awaiter.RegisterWait(msg.MessageId, timeoutMs);

                var stream = SharerdMemoryStreamPool.RentStreamStatic();
                serialiser.EnvelopeMessageWithBytesDontWritePayload(stream, msg, msg.PayloadCount);

                var first = new Segment(stream.GetBuffer(), 0, stream.Position32);
                Segment second;
                if (msg.Payload == null)
                    second = new Segment(new byte[0], 0, 0);
                else
                    second = new Segment(msg.Payload, msg.PayloadOffset, msg.PayloadCount);

                mod[(int)channel].Send(first, second);
                stream.Position32 = 0;
                SharerdMemoryStreamPool.ReturnStreamStatic(stream);
                return task;
            }
            return Task.FromResult(new MessageEnvelope() { Header = MessageEnvelope.RequestCancelled });
        }


        /// <summary>
        /// Sends the reliable message and wait response.
        /// Receiving end must reply with same message id.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="to">To.</param>
        /// <param name="msg">The MSG.</param>
        /// <param name="innerMessage">The inner message.</param>
        /// <param name="timeoutMs">The timeout ms.</param>
        /// <param name="channel">The channel.</param>
        /// <returns></returns>
        public Task<MessageEnvelope> SendRudpMessageAndWaitResponse<T>(Guid to, MessageEnvelope msg, T innerMessage, int timeoutMs = 10000, RudpChannel channel = RudpChannel.Ch1)
        {
            if (TryGetRudpModules(to, out var mod))
            {
                //WriteRudpRouterHeaderIfNeeded(msg, to);
                msg = MessageEnvelope.CloneWithNoRouter(msg);


                msg.MessageId = Guid.NewGuid();
                var task = Awaiter.RegisterWait(msg.MessageId, timeoutMs);

                var stream = SharerdMemoryStreamPool.RentStreamStatic();
                serialiser.EnvelopeMessageWithInnerMessage(stream, msg, innerMessage);
                mod[(int)channel].Send(stream.GetBuffer(), 0, stream.Position32);

                stream.Position32 = 0;
                SharerdMemoryStreamPool.ReturnStreamStatic(stream);
                return task;
            }
            return Task.FromResult(new MessageEnvelope() { Header = MessageEnvelope.RequestCancelled });

        }

        private void WriteRudpRouterHeaderIfNeeded(MessageEnvelope msg,Guid to)
        {
            msg.To = Guid.Empty;
            msg.From = Guid.Empty;
            return;
            if (IsUdpDirectConnectionAvailable(to))
            {
                msg.To = Guid.Empty;
                msg.From = Guid.Empty;
            }
            else
            {
                msg.To = to;
                msg.From = sessionId;
            }
        }
        #endregion Rudp

        #region Interface implementation
        void INetworkNode.SendUdpAsync(IPEndPoint ep, MessageEnvelope message, Action<PooledMemoryStream> callback, ConcurrentAesAlgorithm aesAlgorithm)
        {
            udpServer.TrySendAsync(ep, message, callback, aesAlgorithm, out _,true);
        }

        void INetworkNode.SendUdpAsync(IPEndPoint ep, MessageEnvelope message, Action<PooledMemoryStream> callback)
        {
            udpServer.TrySendAsync(ep, message, callback, out _,true);
        }

        void INetworkNode.SendAsyncMessage(Guid destinatioinId, MessageEnvelope message)
        {
            message.From = sessionId;
            message.To = destinatioinId;
            tcpMessageClient.SendAsyncMessage(message);
            //SendAsyncMessage(destinatioinId, message);
        }

        void INetworkNode.SendAsyncMessage( MessageEnvelope message, Action<PooledMemoryStream>  callback, Guid destinationId)
        {
            message.From = sessionId;
            message.To = destinationId;
            tcpMessageClient.SendAsyncMessage(message,callback);
        }

        private void CheckDisposedAndThrow()
        {
            if (Interlocked.CompareExchange(ref disposed, 0, 0) == 1)
            {
                throw new ObjectDisposedException(nameof(RelayClientBase<S>));
            }
        }
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref disposed, 1, 0) == 0)
            {
                udpServer?.Dispose();
                tcpMessageClient?.Dispose();
                OnPeerRegistered = null;
                OnPeerUnregistered = null;
                OnUdpMessageReceived = null;
                OnMessageReceived = null;
                OnDisconnected = null;

                foreach (var item in RUdpModules)
                {
                    foreach (var m in item.Value)
                    {
                        m?.Release();
                    }
                }
                RUdpModules.Clear();
                foreach (var item in JumboUdpModules)
                {
                    item.Value?.Release();
                }
                JumboUdpModules.Clear();
                foreach (var module in punchedTcpModules)
                {
                    module.Value.Dispose();
                }
                ClearPunchedTcpModules();
            }    
        }

        internal void RegisterTcpNode(IState istate)
        {
            // here we eill register punched tcp endpoint somehow
            var state = (ClientTcpHolepunchState)istate;
            AesTcpModule<S> module;
            if (state.connected)
            {
                 module = new AesTcpModule<S>(state.selfClient, Awaiter,SessionId, state.destinationId);

            }
            else
            {
                 module = new AesTcpModule<S>(state.selfServer, Awaiter, SessionId, state.destinationId);

            }
            module.OnMessageReceived += HandleMessageReceived;
            AddPunchedTcpModule(state.destinationId, module);
          
            Console.WriteLine("REG TCP HP!!");
        }



        #endregion
    }

}
