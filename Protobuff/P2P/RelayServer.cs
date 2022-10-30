using NetworkLibrary.Components;
using NetworkLibrary.TCP.ByteMessage;
using NetworkLibrary.UDP;
using NetworkLibrary.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
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

        private ConcurrentBag<byte[]> bufferPool = new ConcurrentBag<byte[]>();

        //SecureProtoServer tcpServer;
        private AsyncUdpServer udpServer;
        private ConcurrentProtoSerialiser serialiser = new ConcurrentProtoSerialiser();
        private ConcurrentDictionary<Guid, string> RegisteredPeers = new ConcurrentDictionary<Guid, string>();
        private ConcurrentDictionary<Guid, IPEndPoint> RegisteredEndpoints = new ConcurrentDictionary<Guid, IPEndPoint>();
        private ConcurrentDictionary<IPEndPoint, ConcurrentAesAlgorithm> EndpointCryptos = new ConcurrentDictionary<IPEndPoint, ConcurrentAesAlgorithm>();
        private ConcurrentDictionary<Guid, IPEndPoint> HolePunchers = new ConcurrentDictionary<Guid, IPEndPoint>();

        private bool PushPeerList = false;
        private bool shutdown = false;
        private ConcurrentAesAlgorithm relayDectriptor;

        private byte[] ServerUdpInitKey { get; }


        public SecureProtoRelayServer(int port,int maxClients, X509Certificate2 cerificate):base(port, maxClients,cerificate)
        {
            //tcpServer = new SecureProtoServer(port, maxClients,cerificate);
            udpServer = new AsyncUdpServer(port);

            udpServer.OnClientAccepted +=UdpClientAccepted;
            udpServer.OnBytesRecieved +=HandleUdpBytesReceived;
            udpServer.StartServer();


            Task.Run(PushRoutine);

            ServerUdpInitKey  = new Byte[16];
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            rng.GetNonZeroBytes(ServerUdpInitKey);
            relayDectriptor = new ConcurrentAesAlgorithm(ServerUdpInitKey, ServerUdpInitKey);
        }


        public void GetUdpStatistics(out UdpStatistics generalStats, out ConcurrentDictionary<IPEndPoint, UdpStatistics> sessionStats)
        {
            udpServer.GetStatistics(out generalStats,out sessionStats);
        }

        #region Push Updates
        private async void PushRoutine()
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

        protected virtual void NotifyCurrentPeerList(Guid clientId)
        {
            
            var peerlistMsg = new MessageEnvelope();
            peerlistMsg.Header = RelayMessageResources.NotifyPeerListUpdate;

            Dictionary<Guid, string> peerList = new Dictionary<Guid, string>();
            foreach (var peer in RegisteredPeers)
            {
                // exclude self
                if (!peer.Key.Equals(clientId))
                    peerList[peer.Key] = null;
            }

            var listProto = new PeerList<string>();
            listProto.PeerIds = peerList;

            SendAsyncMessage(in clientId, peerlistMsg, peerList);
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
            string clisntIdString = clientId.ToString();
            var requestUdpRegistration = new MessageEnvelope();
            requestUdpRegistration.Header = RelayMessageResources.UdpInit;
            requestUdpRegistration.To = clientId;

            requestUdpRegistration.Payload = ServerUdpInitKey;
            MessageEnvelope udpREsponse = null;
            udpREsponse = await SendMessageAndWaitResponse(clientId, requestUdpRegistration, 10000);

            if (udpREsponse.Header.Equals(MessageEnvelope.RequestTimeout))
                return false;

            IPEndPoint ClientEp = null;
            if (!RegisteredEndpoints.TryGetValue(clientId,out ClientEp))
            {

                var resentInitMessage = new MessageEnvelope();
                resentInitMessage.Header = RelayMessageResources.UdpInitResend;
                resentInitMessage.To = clientId;
                int tries = 0;
                while (!RegisteredEndpoints.TryGetValue(clientId, out ClientEp) && ClientEp == null && tries<500)
                {
                    udpREsponse = await SendMessageAndWaitResponse(clientId, resentInitMessage, 10000);
                    if (udpREsponse.Header.Equals(MessageEnvelope.RequestTimeout))
                        return false;
                    tries++;
                    await Task.Delay(1);
                }

                if (tries > 100)
                {
                    return false;
                }
            }

            var random = new Byte[16];
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            rng.GetNonZeroBytes(random);

            var udpFinalize = new MessageEnvelope();
            udpFinalize.Header = RelayMessageResources.UdpFinaliseInit;
            udpFinalize.To = clientId;
            udpFinalize.Payload = random;

            //var sesAlgo = new AesAlgorithm(random, random);
            try
            {
                EndpointCryptos[ClientEp] = new ConcurrentAesAlgorithm(random,random);

            }
            catch(Exception e)
            {

            }


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
            RegisteredEndpoints.TryRemove(clientId, out _);

            PushPeerList = true;
        }

        protected override void HandleClientAccepted(Guid clientId)
        {
            // todo 
            // countdown client registration time drop if necessary

        }
       
        protected override void OnBytesReceived(in Guid guid, byte[] bytes, int offset, int count)
        {
            MessageEnvelope message = serialiser.DeserialiseOnlyEnvelope(bytes, offset, count);
            bool needsRouting = false;

            if (BitConverter.ToUInt16(bytes, offset) != 0)
            {
                int offset_ = offset;
                int count_ = count;
                serialiser.GetPayloadSlice(bytes, ref offset_, ref count_);
                needsRouting = HandleMessageReceived(in guid, message, bytes, offset_, count_);

            }
            else
            {
                needsRouting = HandleMessageReceived(in guid, message);

            }

            if (!needsRouting)
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
                    default:
                        //RouteMessage(message);
                        return false;
                }
            }
            return true;
                
        }
        bool  HandleMessageReceived(in Guid guid, MessageEnvelope message, byte[] bytes, int offset, int count)
        {
            if (!CheckAwaiter(message))
                return false;
            return true;
                //RouteMessage(message,bytes,offset,count);
        }

        private void RouteMessage(MessageEnvelope message, byte[] bytes, int offset, int count)
        {
            if (RegisteredPeers.TryGetValue(message.To, out _))
                SendAsyncMessage(message.To, message, bytes,offset,count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RouteMessage(MessageEnvelope message)
        {
            if (RegisteredPeers.TryGetValue(message.To, out _))
                SendAsyncMessage(message.To, message);
        }



        private void UdpClientAccepted(SocketAsyncEventArgs ClientSocket)
        {

        }


        private void HandleUdpBytesReceived(IPEndPoint adress, byte[] bytes, int offset, int count)
        {
            // 0 comes when ungracefull shutdown occurs
            if(count == 0)
            {
                udpServer.RemoveClient(adress);
                if (EndpointCryptos.ContainsKey(adress))
                {
                    EndpointCryptos.TryRemove(adress, out _);
                }
               return;
            }

            // Client is trying to register its Udp channel here
            if (!EndpointCryptos.ContainsKey(adress))
            {
                HandleUnregistreredMessage(adress,bytes,offset,count);
                return;
            }

            // decrpy(key of .From) read relay header encrypt(Key of .To)
            if(!bufferPool.TryTake(out var buffer))
            {
                buffer = new byte[65000];
            }
            var algo = EndpointCryptos[adress];
            int decrptedAmount;

            try
            {
                decrptedAmount = algo.DecryptInto(bytes, offset, count, buffer, 0);
            }
            catch
            {
                bufferPool.Add(buffer);
                MiniLogger.Log(MiniLogger.LogLevel.Error, "Udp Relay failed to decrypt peer message");
                return;
            }

            // read only header to route the message.
            MessageEnvelope message = null;
           
            message = serialiser.DeserialiseOnlyEnvelope(buffer,0,decrptedAmount);
            if (RegisteredEndpoints.TryGetValue(message.To, out var destEp))
            {
                if(EndpointCryptos.TryGetValue(destEp, out var encryptor))
                {
                    var reEncryptedBytesAmount = encryptor.EncryptInto(buffer, 0, decrptedAmount, buffer, 0);
                    udpServer.SendBytesToClient(destEp, buffer, 0, reEncryptedBytesAmount);
                }
               
            }
            bufferPool.Add(buffer);
                        
           
        }

        private void HandleUnregistreredMessage(IPEndPoint adress, byte[] bytes, int offset, int count)
        {
            byte[] result = null;
            try
            {
                result = relayDectriptor.Decrypt(bytes, offset, count);
                var message1 = serialiser.DeserialiseEnvelopedMessage(result, 0, result.Length);

                if(message1.Header.Equals(RelayMessageResources.UdpInit))
                    RegisteredEndpoints.TryAdd(message1.From, adress);
                else if (message1.Header.Equals(RelayMessageResources.SendFirstMsgHolePunch))
                {
                    HolePunchers.TryAdd(message1.From, adress);
                }
            }
            catch
            {
                MiniLogger.Log(MiniLogger.LogLevel.Error, "Udp Relay failed to decrypt unregistered peer message");
            }
        }


        /*
         send first message
        dest peer send first message        dest peer hole punch req



         */
        private void HolePunchRoutine(Guid clientId, MessageEnvelope message)
        {
            Task.Run(async () =>
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
                    Header = RelayMessageResources.SendFirstMsgHolePunch,
                    From = message.To,

                };
                Console.WriteLine("First peer create channel req");
                var a = await SendMessageAndWaitResponse(clientId, m0);
                Console.WriteLine("First peer send first req");

                a = await SendMessageAndWaitResponse(clientId, m);
                while (!HolePunchers.ContainsKey(clientId))
                {
                    a = await SendMessageAndWaitResponse(clientId, m);
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
                    Header = RelayMessageResources.SendFirstMsgHolePunch,
                    From = message.From,

                };

                Console.WriteLine("Second peer create first req");

                // request opening new client and then request first message.
                a = await SendMessageAndWaitResponse(message.To, m2);
                Console.WriteLine("First peer send first req");

                a = await SendMessageAndWaitResponse(message.To, mm);
                while (!HolePunchers.ContainsKey(message.To))
                {
                    a = await SendMessageAndWaitResponse(message.To, mm);
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
                t[0] =  SendMessageAndWaitResponse(message.To, m3,1500);
                t[1] =  SendMessageAndWaitResponse(clientId, m4,1500);
                await Task.WhenAll(t);
                if (t[0].Result.Header != MessageEnvelope.RequestTimeout || t[0].Result.Header != MessageEnvelope.RequestTimeout)
                    SendAsyncMessage(clientId, message);
                else
                {
                    SendAsyncMessage(clientId, new MessageEnvelope() { Header = MessageEnvelope.RequestTimeout, MessageId = message.MessageId });
                    HolePunchers.TryRemove(clientId, out _);
                    HolePunchers.TryRemove(message.To, out _);
                }
            });
           

           



        }
    }
}
