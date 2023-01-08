using NetworkLibrary;
using NetworkLibrary.Components;
using NetworkLibrary.Components.Statistics;
using NetworkLibrary.TCP.ByteMessage;
using NetworkLibrary.UDP;
using NetworkLibrary.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Protobuff.P2P
{

    public class SecureProtoRelayServer: SecureProtoServer
    {
        private AsyncUdpServer udpServer;
        private ConcurrentProtoSerialiser serialiser = new ConcurrentProtoSerialiser();
        private ConcurrentDictionary<Guid, string> RegisteredPeers = new ConcurrentDictionary<Guid, string>();
        private ConcurrentDictionary<Guid, IPEndPoint> RegisteredUdpEndpoints = new ConcurrentDictionary<Guid, IPEndPoint>();
        private ConcurrentDictionary<IPEndPoint, ConcurrentAesAlgorithm> UdpCryptos = new ConcurrentDictionary<IPEndPoint, ConcurrentAesAlgorithm>();
        private ConcurrentDictionary<Guid, IPEndPoint> HolePunchers = new ConcurrentDictionary<Guid, IPEndPoint>();

        private bool PushPeerList = false;
        private bool shutdown = false;
        private ConcurrentAesAlgorithm relayDectriptor;

        private byte[] ServerUdpInitKey { get; }


        public SecureProtoRelayServer(int port, X509Certificate2 cerificate):base(port,cerificate)
        {
            udpServer = new AsyncUdpServer(port);

            udpServer.OnClientAccepted +=UdpClientAccepted;
            udpServer.OnBytesRecieved +=HandleUdpBytesReceived;
            udpServer.StartServer();


            Task.Run(PeerListPushRoutine);

            ServerUdpInitKey  = new Byte[16];
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            rng.GetNonZeroBytes(ServerUdpInitKey);
            relayDectriptor = new ConcurrentAesAlgorithm(ServerUdpInitKey, ServerUdpInitKey);
        }

        public void GetTcpStatistics(out TcpStatistics generalStats, out ConcurrentDictionary<Guid, TcpStatistics> sessionStats)
           => GetStatistics(out generalStats, out sessionStats);

        public void GetUdpStatistics(out UdpStatistics generalStats, out ConcurrentDictionary<IPEndPoint, UdpStatistics> sessionStats)
        {
            udpServer.GetStatistics(out generalStats,out sessionStats);
        }

        public bool TryGetClientId( IPEndPoint ep, out Guid id)
        {
            id = RegisteredUdpEndpoints.Where(x=>x.Value == ep).Select(x=>x.Key).FirstOrDefault();
            return id != default;
        }
        #region Push Updates
        private async Task PeerListPushRoutine()
        {
            while (!shutdown)
            {
                await Task.Delay(3000);
                if (PushPeerList)
                {
                    PushPeerList = false;
                    foreach (var item in RegisteredPeers)
                    {
                        NotifyCurrentPeerList(item.Key);
                    }
                }
                
            }
        }

        // this needs to be overridable
        protected virtual void NotifyCurrentPeerList(Guid clientId)
        {

            var peerlistMsg = new MessageEnvelope();
            peerlistMsg.Header = RelayMessageResources.NotifyPeerListUpdate;

            var peerList = new Dictionary<Guid, PeerInfo>();
            // enumerating concurrent dict suppose to be thread safe but 
            // peer KV pair here can sometimes be null... 
            foreach (var peer in RegisteredPeers)
            {
                try
                {
                    // exclude self
                    if (!peer.Key.Equals(clientId))
                        peerList[peer.Key] = new PeerInfo()
                        {
                            IP = GetIPEndPoint(peer.Key).Address.ToString(),
                            Port = GetIPEndPoint(peer.Key).Port
                        };
                }
                catch { PushPeerList = true; continue; }
               
            }

            var listProto = new PeerList<PeerInfo>();
            listProto.PeerIds = peerList;

            SendAsyncMessage(in clientId, peerlistMsg, listProto);
        }


        #endregion Push Updates

        #region Registration

        private void InitiateRegisteryRoutine(Guid clientId, MessageEnvelope message)
        {
            Task.Run(async () =>
            {
                if (VerifyClientCredentials(clientId, message))
                {
                    bool res = await GenerateSeureUdpChannel(clientId);
                    if (res == false)
                        return;

                    MessageEnvelope reply = new MessageEnvelope();
                    reply.Header = RelayMessageResources.RegisterySucces;
                    reply.To = clientId;
                    reply.MessageId = message.MessageId;

                    SendAsyncMessage(in clientId, reply);

                    MessageEnvelope requestAck = new MessageEnvelope();
                    requestAck.Header = RelayMessageResources.RegisteryAck;
                    var ackResponse = await SendMessageAndWaitResponse(clientId, requestAck, 10000);

                    if (ackResponse.Header != MessageEnvelope.RequestTimeout)
                    {
                        RegisterPeer(in clientId);
                    }
                }
                else
                {
                    MessageEnvelope reply = new MessageEnvelope();
                    reply.Header = RelayMessageResources.RegisteryFail;
                    reply.To = clientId;
                    reply.MessageId = message.MessageId;

                    SendAsyncMessage(in clientId, reply);
                }

            });

            
        }

        protected virtual async Task<bool> GenerateSeureUdpChannel(Guid clientId)
        {
            var requestUdpRegistration = new MessageEnvelope();
            requestUdpRegistration.Header = RelayMessageResources.UdpInit;
            requestUdpRegistration.To = clientId;
            requestUdpRegistration.Payload = ServerUdpInitKey;

            var udpREsponse = await SendMessageAndWaitResponse(clientId, requestUdpRegistration, 10000);
            if (udpREsponse.Header.Equals(MessageEnvelope.RequestTimeout)) return false;

            IPEndPoint ClientEp = null;
            if (!RegisteredUdpEndpoints.TryGetValue(clientId,out ClientEp))
            {
                var resentInitMessage = new MessageEnvelope();
                resentInitMessage.Header = RelayMessageResources.UdpInitResend;
                resentInitMessage.To = clientId;

                int tries = 0;
                while (!RegisteredUdpEndpoints.TryGetValue(clientId, out ClientEp) && ClientEp == null && tries<500)
                {
                    udpREsponse = await SendMessageAndWaitResponse(clientId, resentInitMessage, 10000);
                    if (udpREsponse.Header.Equals(MessageEnvelope.RequestTimeout)) return false;

                    tries++;
                    await Task.Delay(1);
                }

                if (tries > 100)
                {
                    return false;
                }
            }

            var random = new byte[16];
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            rng.GetNonZeroBytes(random);

            var udpFinalize = new MessageEnvelope();
            udpFinalize.Header = RelayMessageResources.UdpFinaliseInit;
            udpFinalize.To = clientId;
            udpFinalize.Payload = random;

            UdpCryptos[ClientEp] = new ConcurrentAesAlgorithm(random,random);

            
            udpREsponse = await SendMessageAndWaitResponse(clientId, udpFinalize, 10000);
            if (udpREsponse.Header.Equals(MessageEnvelope.RequestTimeout))
                return false;
            return true;

        }
        protected virtual bool VerifyClientCredentials(Guid clientId, MessageEnvelope message)
        {
            return true;
        }

        private void RegisterPeer(in Guid clientId)
        {
            RegisteredPeers[clientId] = null;
            PushPeerList = true;
        }

        #endregion Registration

        protected override void HandleClientDisconnected(Guid clientId)
        {
            RegisteredPeers.TryRemove(clientId, out _);
            if(RegisteredUdpEndpoints.TryRemove(clientId, out var key))
                UdpCryptos.TryRemove(key, out _);

            PushPeerList = true;
        }

        protected override void HandleClientAccepted(Guid clientId)
        {
            // todo 
            // countdown client registration time drop if necessary

        }
       
        // Goal is to only read envelope and route with that, without unpcaking payload.
        protected override void OnBytesReceived(in Guid guid, byte[] bytes, int offset, int count)
        {
            MessageEnvelope message = serialiser.DeserialiseOnlyEnvelope(bytes, offset, count);
            bool dontRoute = false;

            // Check if it has payload
            if (BitConverter.ToUInt16(bytes, offset) != 0)
            {
                int offset_ = offset;
                int count_ = count;
                serialiser.TryGetPayloadSlice(bytes, ref offset_, ref count_);
                dontRoute = HandleMessageReceived(in guid, message, bytes, offset_, count_);

            }
            else
            {
                dontRoute = HandleMessageReceived(in guid, message);

            }

            if (!dontRoute)
            {
                server.SendBytesToClient(message.To, bytes, offset, count);
            }

              
        }

        bool HandleMessageReceived(in Guid clientId, MessageEnvelope message)
        {
            if (!CheckAwaiter(message))
            {
                switch (message.Header)
                {
                    case (RelayMessageResources.RequestRegistery):
                        InitiateRegisteryRoutine(clientId, message);
                        return true;
                    case (RelayMessageResources.HolePunchRegister):
                        HolePunchRoutine(clientId, message);
                        return true;
                    case "WhatsMyIp":
                        SendEndpoint(clientId,message);
                        return true;
                    default:
                        return false;
                }
            }
            return true;
                
        }

        private void SendEndpoint(Guid clientId, MessageEnvelope message)
        {
            var info = new PeerInfo()
            {
                IP = GetIPEndPoint(clientId).Address.ToString(),
                Port = GetIPEndPoint(clientId).Port
            };

            var env = new MessageEnvelope();
            env.MessageId= message.MessageId;
            SendAsyncMessage(clientId, env, info);
        }

        bool  HandleMessageReceived(in Guid guid, MessageEnvelope message, byte[] bytes, int offset, int count)
        {
            if (!CheckAwaiter(message))
                return false;
            return true;
                //RouteMessage(message,bytes,offset,count);
        }

      
        private void UdpClientAccepted(SocketAsyncEventArgs ClientSocket)
        {

        }


        private void HandleUdpBytesReceived(IPEndPoint adress, byte[] bytes, int offset, int count)
        {

            // Client is trying to register its Udp endpoint here
            if (!UdpCryptos.ContainsKey(adress))
            {
                HandleUnregistreredMessage(adress,bytes,offset,count);
                return;
            }

            // we need to find the crypto of sender and critptto of receiver
            // decrpy(key of .From) read relay header, encrypt(Key of .To)
            if (!UdpCryptos.TryGetValue(adress, out var algo))
            {
                return;
            }

            var buffer = BufferPool.RentBuffer(count+256);
            try
            {
                int decrptedAmount = algo.DecryptInto(bytes, offset, count, buffer, 0);

                // only read header to route the message.
                var message = serialiser.DeserialiseOnlyEnvelope(buffer, 0, decrptedAmount);
                if (RegisteredUdpEndpoints.TryGetValue(message.To, out var destEp))
                {
                    if (UdpCryptos.TryGetValue(destEp, out var encryptor))
                    {
                        var reEncryptedBytesAmount = encryptor.EncryptInto(buffer, 0, decrptedAmount, buffer, 0);
                        udpServer.SendBytesToClient(destEp, buffer, 0, reEncryptedBytesAmount);
                    }

                }
            }
            catch
            {
                MiniLogger.Log(MiniLogger.LogLevel.Error, "Udp Relay failed to deserialise envelope message ");
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

                if (message1.Header.Equals(RelayMessageResources.UdpInit))
                {
                    RegisteredUdpEndpoints.TryAdd(message1.From, adress);
                }
                else if (message1.Header.Equals(RelayMessageResources.RegisterHolePunchEndpoint))
                {
                    HolePunchers.TryAdd(message1.From, adress);
                }
            }
            catch
            {
                MiniLogger.Log(MiniLogger.LogLevel.Error, "Udp Relay failed to decrypt unregistered peer message");
            }
        }


        private void HolePunchRoutine(Guid clientId, MessageEnvelope message)
        {
            Task.Run(async () =>
            {
                try
                {
                    string requesterId = clientId.ToString();

                    MessageEnvelope m0 = new MessageEnvelope()
                    {
                        Header = RelayMessageResources.HolePunchRequest,
                        From = message.To,
                        Payload = ServerUdpInitKey,
                    };
                    MessageEnvelope m = new MessageEnvelope()
                    {
                        Header = RelayMessageResources.RegisterHolePunchEndpoint,
                        From = message.To,

                    };

                    Console.WriteLine("First peer create channel req");
                    var a = await SendMessageAndWaitResponse(clientId, m0);
                    if (a.Header == MessageEnvelope.RequestTimeout) return;

                    Console.WriteLine("First peer send first req");
                    a = await SendMessageAndWaitResponse(clientId, m);
                    if (a.Header == MessageEnvelope.RequestTimeout) return;

                    int dropCount = 0;
                    while (!HolePunchers.ContainsKey(clientId) && dropCount < 5)
                    {
                        dropCount++;
                        a = await SendMessageAndWaitResponse(clientId, m);
                        if (a.Header == MessageEnvelope.RequestTimeout) return;
                    }

                    // got first message from requester i know his ep now.
                    MessageEnvelope m2 = new MessageEnvelope()
                    {
                        Header = RelayMessageResources.HolePunchRequest,
                        From = message.From,
                        Payload = ServerUdpInitKey,

                    };

                    MessageEnvelope mm = new MessageEnvelope()
                    {
                        Header = RelayMessageResources.RegisterHolePunchEndpoint,
                        From = message.From,

                    };


                    // request opening new client and then request first message.
                    Console.WriteLine("Second peer create first req");
                    a = await SendMessageAndWaitResponse(message.To, m2);
                    if (a.Header == MessageEnvelope.RequestTimeout) return;

                    Console.WriteLine("First peer send first req");
                    a = await SendMessageAndWaitResponse(message.To, mm);
                    if (a.Header == MessageEnvelope.RequestTimeout) return;

                    dropCount = 0;
                    while (!HolePunchers.ContainsKey(message.To) && dropCount < 5)
                    {
                        dropCount++;
                        a = await SendMessageAndWaitResponse(message.To, mm);
                        if (a.Header == MessageEnvelope.RequestTimeout) return;

                    }

                    // we got the endpoints of both now, relay that enfo to peers.
                    // the moment they get this message they will send udp messages to each toher and will reply to me
                    string ip = HolePunchers[message.From].Address.ToString();
                    string port = HolePunchers[message.From].Port.ToString();
                    Console.WriteLine(ip + " ; " + port);

                    MessageEnvelope m3 = new MessageEnvelope()
                    {
                        Header = RelayMessageResources.EndpointTransfer,
                        From = message.From,
                        KeyValuePairs = new Dictionary<string, string>()
                        {
                            {ip,port }
                        }

                    };

                    string ip2 = HolePunchers[message.To].Address.ToString();
                    string port2 = HolePunchers[message.To].Port.ToString();

                    Console.WriteLine(ip2 + " ; " + port2);

                    MessageEnvelope m4 = new MessageEnvelope()
                    {
                        Header = RelayMessageResources.EndpointTransfer,
                        From = message.To,
                        KeyValuePairs = new Dictionary<string, string>()
                        {
                            {ip2,port2 }
                        }

                    };


                    Task<MessageEnvelope>[] t = new Task<MessageEnvelope>[2];
                    t[0] = SendMessageAndWaitResponse(message.To, m3, 4000);
                    t[1] = SendMessageAndWaitResponse(clientId, m4, 4000);
                    await Task.WhenAll(t);
                    if (t[0].Result.Header != MessageEnvelope.RequestTimeout && t[1].Result.Header != MessageEnvelope.RequestTimeout)
                        SendAsyncMessage(clientId, message);
                    else
                    {
                        SendAsyncMessage(clientId, new MessageEnvelope() { Header = MessageEnvelope.RequestTimeout, MessageId = message.MessageId });
                        HolePunchers.TryRemove(clientId, out _);
                        HolePunchers.TryRemove(message.To, out _);
                    }
                }
                catch(Exception e)
                {
                    MiniLogger.Log(MiniLogger.LogLevel.Error, "While Punching Hole Error Occurred: "+e.Message);
                }
                finally
                {
                    HolePunchers.TryRemove(clientId, out _);
                    HolePunchers.TryRemove(message.To, out _);
                }
                
            });
           
        }
       

    }
}
