using NetworkLibrary.Components;
using NetworkLibrary.UDP.Secure;
using NetworkLibrary.Utils;
using Protobuff.Components;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Protobuff.P2P
{
    public class RelayClient
    {
        public Action<Guid> OnPeerRegistered;
        public Action<Guid> OnPeerUnregistered;
        public Action<MessageEnvelope> OnUdpMessageReceived;
        public Action<MessageEnvelope> OnMessageReceived;
        public Action OnDisconnected;
        public RemoteCertificateValidationCallback RemoteCertificateValidationCallback => protoClient.RemoteCertificateValidationCallback;
        private SecureProtoClient protoClient;
        private ConcurrentProtoSerialiser serialiser = new ConcurrentProtoSerialiser();
        public Guid sessionId { get; private set; }
        public HashSet<Guid> Peers = new HashSet<Guid>();

        private ConcurrentDictionary<Guid,EncryptedUdpProtoClient> holePunchCandidates = new ConcurrentDictionary<Guid,EncryptedUdpProtoClient>();
        private ConcurrentDictionary<Guid,EncryptedUdpProtoClient> directUdpClients = new ConcurrentDictionary<Guid,EncryptedUdpProtoClient>();
        private ConcurrentDictionary<Guid, TaskCompletionSource<bool>> awaitingUdpPunchMessage = new ConcurrentDictionary<Guid, TaskCompletionSource<bool>>();

        private EncryptedUdpProtoClient udpRelayClient;
        private bool connecting;
        public bool IsConnected { get; private set; }
        internal ConcurrentDictionary<Guid, PeerInfo> PeerInfos { get; private set; } = new ConcurrentDictionary<Guid, PeerInfo>();

        private string connectHost;
        private int connectPort;
        private PingHandler pinger = new PingHandler();
    
        public RelayClient(X509Certificate2 clientCert)
        {
            protoClient = new SecureProtoClient(clientCert);
            protoClient.OnMessageReceived += HandleMessageReceived;
            protoClient.OnDisconnected += HandleDisconnect;
            

        }

        public void StartPingService()
        {
            Task.Run(() => SendPing());
        }


        #region Connect & Disconnect

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Connect(string host, int port)
        {
            if (connecting || IsConnected) return;

            connectHost = host;
            connectPort = port;
            connecting = true;
            try
            {
                protoClient.Connect(host, port);
                RegisterRoutine();
                IsConnected= true;
                pinger.PeerRegistered(sessionId);

            }
            catch { throw; }
            finally
            {
                connecting = false;
            }
           


        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task<bool> ConnectAsync(string host, int port)
        {
            if (connecting || IsConnected) return false;

            try
            {
                connecting = true;

                connectHost = host;
                connectPort = port;
                await protoClient.ConnectAsync(host, port);

                var requestRegistery = new MessageEnvelope();
                requestRegistery.Header = RelayMessageResources.RequestRegistery;

                var response = await protoClient.SendMessageAndWaitResponse(requestRegistery, 20000);

                connecting = false;


                if (response.Header == MessageEnvelope.RequestTimeout ||
                    response.Header == RelayMessageResources.RegisteryFail)
                    throw new TimeoutException(response.Header);

                this.sessionId = response.To;
                IsConnected = true;
                pinger.PeerRegistered(sessionId);

                return true;
            }
            catch
            {
                throw;
            }
            finally
            {
                connecting = false;
            }
          

        }
        public void Disconnect()
        {
            protoClient.Disconnect();
        }

        private void HandleDisconnect()
        {
            IsConnected = false;
            OnDisconnected?.Invoke();
        }

        #endregion


        protected virtual void RegisterRoutine()
        {
            var requestRegistery = new MessageEnvelope();
            requestRegistery.Header = RelayMessageResources.RequestRegistery;

            var response = protoClient.SendMessageAndWaitResponse(requestRegistery,20000).Result;

            if (response.Header == MessageEnvelope.RequestTimeout ||
                response.Header == RelayMessageResources.RegisteryFail)
                throw new TimeoutException(response.Header);

            this.sessionId = response.To;
        }

        #region Ping
        private async void SendPing()
        {
            while (true)
            {
                await Task.Delay(500);

                MessageEnvelope msg = new MessageEnvelope();
                msg.Header = PingHandler.Ping;
                if (IsConnected)
                {
                    msg.TimeStamp = DateTime.Now;
                    msg.From = sessionId;
                    msg.To = sessionId;
                    protoClient.SendAsyncMessage(msg);

                    SendUdpMesssage(sessionId, msg);
                    pinger.NotifyTcpPingSent(sessionId, msg.TimeStamp);
                    pinger.NotifyUdpPingSent(sessionId, msg.TimeStamp);

                    await Task.Delay(500);
                    var peerSnapshot = Peers.ToArray();
                    foreach (var peer in peerSnapshot)
                    {
                        msg.TimeStamp = DateTime.Now;
                        SendAsyncMessage(peer, msg);
                        SendUdpMesssage(peer, msg);
                        pinger.NotifyTcpPingSent(peer, msg.TimeStamp);
                        pinger.NotifyUdpPingSent(peer, msg.TimeStamp);
                    }
                }
               
               
                
            }

        }

        private void HandlePing(MessageEnvelope message, bool isTcp = true)
        {
            // self ping - server roundtrip.
            if(message.From == sessionId)
            {
                //HandlePong(message,isTcp);
                if (isTcp) pinger.HandleTcpPongMessage(message);
                else pinger.HandleUdpPongMessage(message);
            }
            else
            {
                message.Header = PingHandler.Pong;
                if (isTcp)
                {
                    message.To = message.From;
                    message.From = sessionId;
                    protoClient.SendAsyncMessage(message);
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

        public Dictionary<Guid,double> GetTcpPingStatus()
        {
            return pinger.GetTcpLatencies();
        }
        public Dictionary<Guid, double> GetUdpPingStatus()
        {
            return pinger.GetUdpLatencies();
        }

        #endregion Ping

        #region Send
        public void SendUdpMesssage<T>(Guid toId, T message, string messageHeader = null) where T : IProtoMessage
        {
            if (!Peers.Contains(toId) && toId != sessionId)
                return;
            MessageEnvelope env = new MessageEnvelope();
            env.From = sessionId;
            env.To = toId;
            env.Header = messageHeader == null ? typeof(T).Name : messageHeader;

            if (directUdpClients.TryGetValue(toId, out var client))
                client.SendAsyncMessage(env, message);
            else
                udpRelayClient.SendAsyncMessage(env, message);
        }

        public void SendUdpMesssage(Guid toId, MessageEnvelope message)
        {
            if (!Peers.Contains(toId) && toId != sessionId)
                return;
            message.From = sessionId;
            message.To = toId;

            if (directUdpClients.TryGetValue(toId, out var client))
                client.SendAsyncMessage(message);
            else
                udpRelayClient.SendAsyncMessage(message);
        }
        public void SendUdpMesssage(Guid toId, byte[] data, int offset, int count, string dataName)
        {
            if (!Peers.Contains(toId) && toId != sessionId)
                return;

            MessageEnvelope env = new MessageEnvelope();
            env.From = sessionId;
            env.To = toId;
            env.Header = dataName;

            if (directUdpClients.TryGetValue(toId, out var client))
                client.SendAsyncMessage(env, data, offset, count);
            else
                udpRelayClient.SendAsyncMessage(env, data, offset, count);
        }

        public void SendUdpMesssage(Guid toId, byte[] data, string dataName)
        {
            if (!Peers.Contains(toId) && toId != sessionId)
                return;

            var envelopedMessage = RelayMessageResources.MakeRelayMessage(sessionId, toId, null);
            envelopedMessage.Header = dataName;

            if (directUdpClients.TryGetValue(toId, out var client))
                client.SendAsyncMessage(envelopedMessage, data, 0, data.Length);
            else
                udpRelayClient.SendAsyncMessage(envelopedMessage, data, 0, data.Length);
        }
        public void SendAsyncMessage(Guid toId, MessageEnvelope message)
        {
            if (!Peers.Contains(toId) && toId != sessionId)
                return;

            message.From = sessionId;
            message.To = toId;
            protoClient.SendAsyncMessage(message);
        }
        public void SendAsyncMessage<T>(Guid toId, T message,string messageHeader=null) where T : IProtoMessage
        {
            if (!Peers.Contains(toId) && toId != sessionId)
                return;

            var envelopedMessage = RelayMessageResources.MakeRelayMessage(sessionId, toId, null);
            envelopedMessage.Header = messageHeader == null?typeof(T).Name: messageHeader;
            protoClient.SendAsyncMessage(envelopedMessage, message);
        }
        public void SendAsyncMessage(Guid toId, byte[] data, string dataName)
        {
            if (!Peers.Contains(toId) && toId != sessionId)
                return;

            var envelopedMessage = RelayMessageResources.MakeRelayMessage(sessionId, toId, null);
            envelopedMessage.Header = dataName;
            protoClient.SendAsyncMessage(envelopedMessage, data, 0, data.Length);
        }

        public void SendAsyncMessage(Guid toId, byte[] data, int offset, int count, string dataName)
        {
            if (!Peers.Contains(toId) && toId != sessionId)
                return;

            var envelopedMessage = RelayMessageResources.MakeRelayMessage(sessionId, toId, null);
            envelopedMessage.Header = dataName;
            protoClient.SendAsyncMessage(envelopedMessage, data, offset, count);
        }

        public async Task<MessageEnvelope> SendRequestAndWaitResponse<T>(Guid toId, T message,string messageHeader = null, int timeoutMs = 10000) where T : IProtoMessage
        {

            var envelopedMessage = RelayMessageResources.MakeRelayRequestMessage(Guid.NewGuid(), sessionId, toId, null);
            envelopedMessage.Header = messageHeader == null ? typeof(T).Name : messageHeader;

            var result = await protoClient.SendMessageAndWaitResponse(envelopedMessage, message, timeoutMs);
            return result;
        }

        public async Task<MessageEnvelope> SendRequestAndWaitResponse(Guid toId, byte[] data, string dataName, int timeoutMs = 10000)
        {

            var envelopedMessage = RelayMessageResources.MakeRelayRequestMessage(Guid.NewGuid(), sessionId, toId, null);
            envelopedMessage.Header = dataName;

            var response = await protoClient.SendMessageAndWaitResponse(envelopedMessage, data, 0, data.Length, timeoutMs = 10000);
            return response;
        }
        public async Task<MessageEnvelope> SendRequestAndWaitResponse(Guid toId, MessageEnvelope message, int timeoutMs = 10000)
        {
            message.From = sessionId;
            message.To = toId;

            var response = await protoClient.SendMessageAndWaitResponse(message, timeoutMs);
            return response;
        }


        #endregion

        private void HandleMessageReceived(MessageEnvelope message)
        {
            //if (Peers.Contains(message.From))

            switch (message.Header)
            {
                case RelayMessageResources.RegisteryAck:
                    protoClient.SendAsyncMessage(message);
                    break;

                case RelayMessageResources.UdpInit:
                    CreateUdpChannel(message);
                    break;

                case RelayMessageResources.UdpInitResend:
                    ResendUdpInitMessage(message);
                    break;

                case RelayMessageResources.UdpFinaliseInit:
                    FinalizeUdpInit(message);
                    break;

                case RelayMessageResources.HolePunchRequest:
                    CreateHolePunchChannel(message);
                    break;

                case RelayMessageResources.RegisterHolePunchEndpoint:
                    RegisterRemoteEndpoint(message);
                    break;

                case RelayMessageResources.EndpointTransfer:
                    StartPunching(message);
                    break;

                case (RelayMessageResources.NotifyPeerListUpdate):
                    UpdatePeerList(message);
                    break;

                case PingHandler.Ping:
                    HandlePing(message);
                    break;

                case PingHandler.Pong:
                    HandlePong(message);
                    break;

                default:
                    OnMessageReceived?.Invoke(message);
                    break;
            }

        }

      

        #region Relay Udp Channel Creation
        private void CreateUdpChannel(MessageEnvelope message)
        {
            ConcurrentAesAlgorithm algo = new ConcurrentAesAlgorithm(message.Payload, message.Payload);
            udpRelayClient = new EncryptedUdpProtoClient(algo);
            udpRelayClient.SocketSendBufferSize = 1280000;
            udpRelayClient.ReceiveBufferSize = 12800000;
            udpRelayClient.OnMessageReceived += HandleUdpMessageReceived;
            udpRelayClient.Bind();
            udpRelayClient.SetRemoteEnd(connectHost, connectPort);

            message.From = message.To;
            message.Payload = null;

            var bytes = serialiser.SerialiseEnvelopedMessage(message);
            udpRelayClient.SendAsync(bytes);

            protoClient.SendAsyncMessage(message);
            Console.WriteLine("Created channel responding..");

        }


        private void ResendUdpInitMessage(MessageEnvelope message)
        {
            message.Header = RelayMessageResources.UdpInit;
            message.From = message.To;
            message.Payload = null;

            udpRelayClient.SendAsyncMessage(message);

            protoClient.SendAsyncMessage(message);
        }

        private void FinalizeUdpInit(MessageEnvelope message)
        {

            ConcurrentAesAlgorithm algo = new ConcurrentAesAlgorithm(message.Payload, message.Payload);
            udpRelayClient.SwapAlgorith(algo);
            protoClient.SendAsyncMessage(message);
        }

        #endregion

        #region Hole Punch
        public bool RequestHolePunch(Guid peerId, int timeOut = 10000)
        {
            var requerstHolePunch = new MessageEnvelope();
            requerstHolePunch.Header = RelayMessageResources.HolePunchRegister;
            requerstHolePunch.From = sessionId;
            requerstHolePunch.To = peerId;

            var a = protoClient.SendMessageAndWaitResponse(requerstHolePunch, timeOut).Result;
            if (a.Header != MessageEnvelope.RequestTimeout)
            {
                MiniLogger.Log(MiniLogger.LogLevel.Info,"Sucessfully punched hole");
                return true;
            }
            else
            {
                MiniLogger.Log(MiniLogger.LogLevel.Info, "Hole Punch Failed");
                return false;
            }
        }
        // Ask the server about holepunch
        public async Task<bool> RequestHolePunchAsync(Guid peerId, int timeOut)
        {
            var requerstHolePunch = new MessageEnvelope();
            requerstHolePunch.Header = RelayMessageResources.HolePunchRegister;
            requerstHolePunch.From = sessionId;
            requerstHolePunch.To = peerId;

            var a = await protoClient.SendMessageAndWaitResponse(requerstHolePunch, timeOut);
            if (a.Header != MessageEnvelope.RequestTimeout)
            {
                MiniLogger.Log(MiniLogger.LogLevel.Info, "Sucessfully punched hole");
                return true;
            }
            else
            {
                MiniLogger.Log(MiniLogger.LogLevel.Info, "Hole Punch Failed");
                return false;
            }
        }

        // Server will tell you to make a udp client to create holepunch channel.
        // at the same time other peer gets this message aswell.
        private void CreateHolePunchChannel(MessageEnvelope message)
        {
            var udpClient = new EncryptedUdpProtoClient(new ConcurrentAesAlgorithm(message.Payload, message.Payload));
            udpClient.SocketSendBufferSize = 12800000;
            udpClient.ReceiveBufferSize = 12800000;
            udpClient.OnMessageReceived += HolePunchMsgReceived;
            udpClient.Bind();

            // tricky point: its disaster when udp client receives from 2 endpoints.. corruption
            udpClient.SetRemoteEnd(connectHost, connectPort, receive: false);
            holePunchCandidates[message.From] = udpClient;

            message.Header = RelayMessageResources.RegisterHolePunchEndpoint;
            message.From = sessionId;
            message.Payload = null;

            var bytes = serialiser.SerialiseEnvelopedMessage(message);
            Console.WriteLine("Sending channelCreation message");
            udpClient.SendAsync(bytes);

            protoClient.SendAsyncMessage(message);

        }

        //Server here asks you to send a message so he can infer your remote endpoint,
        // at the same time other peer gets this message aswell.
        private void RegisterRemoteEndpoint(MessageEnvelope message)
        {
            Guid from = message.From;
            message.Header = RelayMessageResources.RegisterHolePunchEndpoint;
            message.From = sessionId;
            message.Payload = null;

            Console.WriteLine("Sending first message");

            holePunchCandidates[from].SendAsyncMessage(message);

            protoClient.SendAsyncMessage(message);
        }

        // Here server will tell you the target endpoint of your peer so you send udp messages here.
        private void StartPunching(MessageEnvelope message)
        {
            Console.WriteLine("Puncing requested");

            Guid from = message.From;
            var udpClient = holePunchCandidates[message.From];

            string Ip = message.KeyValuePairs.Keys.First();
            int port = int.Parse(message.KeyValuePairs[Ip]);

            udpClient.SetRemoteEnd(Ip,port);

           // create temp awaiter
            var udpRec = new TaskCompletionSource<bool>();
            awaitingUdpPunchMessage[from] = udpRec;
            Task.Run(async () =>
            {
                await Task.WhenAny(udpRec.Task, Task.Delay(2000));
                if (udpRec.Task.IsCompleted && udpRec.Task.Result == true)
                {
                    awaitingUdpPunchMessage.TryRemove(from, out _);
                    // notify relay
                    protoClient.SendAsyncMessage(message);
                    // remove the temp client
                    holePunchCandidates.TryRemove(from, out var hole);

                    // add it to directUdpClients so we use this from now on instead of relay channel.
                    hole.OnMessageReceived +=HandleUdpMessageReceived;
                    directUdpClients[from] = hole;
                }
                else
                {
                    // fail
                    Console.WriteLine("Fail..");
                    if(holePunchCandidates.TryRemove(from, out var client))
                    {
                        client.Dispose();
                    }
                }
                
            });
            // messages needs to go asap.
            // todo key not found
            try
            {
                Console.WriteLine("Puncing..");
                message.From = sessionId;
                holePunchCandidates[from].SendAsyncMessage(message);
                holePunchCandidates[from].SendAsyncMessage(message);

                Task.Run(async () =>
                {
                    for (int i = 0; i < 30; i++)
                    {
                        holePunchCandidates[from].SendAsyncMessage(message);
                        if (i > 10)
                            await Task.Delay(i - 10);

                    }
                });
            }
            catch { }
            

        }

        private void HolePunchMsgReceived(MessageEnvelope obj)
        {
            if (awaitingUdpPunchMessage.TryGetValue(obj.From, out var tcs))
            {
                 tcs.TrySetResult(true);
            }
        }

        #endregion

        private void HandleUdpMessageReceived(MessageEnvelope message)
        {
            if (message.Header.Equals(RelayMessageResources.EndpointTransfer)) { }
            else if (message.Header.Equals(PingHandler.Ping)) HandlePing(message, isTcp: false);
            else if (message.Header.Equals(PingHandler.Pong)) HandlePong(message, isTcp: false);

            else OnUdpMessageReceived?.Invoke(message);
        }
        public PeerInfo GetPeerInfo(Guid peerId)
        {
            PeerInfos.TryGetValue(peerId, out var val);
            return val;
        }

        protected virtual void UpdatePeerList(MessageEnvelope message)
        {
            if(message.Payload== null)
                return;

            PeerList<PeerInfo> pperInfoList =  serialiser.Deserialize<PeerList<PeerInfo>>(message.Payload, 0, message.Payload.Length);
            HashSet<Guid> serverSet;
            if (pperInfoList.PeerIds == null || pperInfoList.PeerIds.Count == 0)
            {
                 serverSet = new HashSet<Guid>();
            }
            else
            {
                 serverSet = new HashSet<Guid>(pperInfoList.PeerIds.Keys);
            }

            foreach (var peer in Peers)
            {
                if (!serverSet.Contains(peer))
                {
                    try
                    {
                        OnPeerUnregistered?.Invoke(peer);
                        pinger.PeerUnregistered(peer);
                        PeerInfos.TryRemove(peer, out _);
                    }
                    catch { }
                   
                }
            }

            foreach (var peer in serverSet)
            {
                if (!Peers.Contains(peer))
                {
                    try
                    {
                        OnPeerRegistered?.Invoke(peer);
                        pinger.PeerRegistered(peer);
                        PeerInfos.TryAdd(peer, pperInfoList.PeerIds[peer]);
                    }
                    catch { }
                   

                }
            }
            Peers.UnionWith(serverSet);
            Peers.RemoveWhere(x => !serverSet.Contains(x));

        }

    }
}
