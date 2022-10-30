using NetworkLibrary.Components;
using NetworkLibrary.UDP.Secure;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
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


        private SecureProtoClient protoClient;
        private ConcurrentProtoSerialiser serialiser = new ConcurrentProtoSerialiser();
        public Guid sessionId { get; private set; }
        public HashSet<Guid> Peers = new HashSet<Guid>();
        private ConcurrentDictionary<Guid,EncryptedUdpProtoClient> udpClients = new ConcurrentDictionary<Guid,EncryptedUdpProtoClient>();

        private EncryptedUdpProtoClient udpClient;
        private bool connecting;
        public bool IsConnected { get; private set; }
        private string connectHost;
        private int connectPort;
    
        TaskCompletionSource<bool> udpRec = new TaskCompletionSource<bool>();
        public RelayClient(X509Certificate2 clientCert)
        {
            protoClient = new SecureProtoClient(clientCert);
            protoClient.OnMessageReceived += HandleStatusMessageReceived;
            protoClient.OnDisconnected += OnDc;
        }

        private void OnDc()
        {
            IsConnected = false;
            OnDisconnected?.Invoke();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Connect(string host, int port)
        {
            connectHost = host;
            connectPort = port;
            protoClient.Connect(host, port);
            RegisterRoutine();

        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task<bool> ConnectAsync(string host, int port)
        {
            if (connecting || IsConnected)
                return false;
            try
            {
                connecting = true;

                connectHost = host;
                connectPort = port;
                protoClient.Connect(host, port);

                var requestRegistery = new MessageEnvelope();
                requestRegistery.Header = RelayMessageResources.RequestRegistery;

                var response = await protoClient.SendMessageAndWaitResponse(requestRegistery, 20000);

                connecting = false;


                if (response.Header == MessageEnvelope.RequestTimeout ||
                    response.Header == RelayMessageResources.RegisteryFail)
                    throw new IOException(response.Header);

                this.sessionId = response.To;
                IsConnected = true;
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        protected virtual void RegisterRoutine()
        {
            var requestRegistery = new MessageEnvelope();
            requestRegistery.Header = RelayMessageResources.RequestRegistery;

            var response = protoClient.SendMessageAndWaitResponse(requestRegistery,20000).Result;
            if (response.Header == MessageEnvelope.RequestTimeout ||
                response.Header == RelayMessageResources.RegisteryFail)
                throw new IOException(response.Header);
            this.sessionId = response.To;
        }
       

        public void Disconnect()
        {
            protoClient.Disconnect();
        }

        public bool RequestHolePunch(Guid peerId, int timeOut = 10000)
        {
            var requerstHolePunch = new MessageEnvelope();
            requerstHolePunch.Header = RelayMessageResources.HolePunchRegister;
            requerstHolePunch.From = sessionId;
            requerstHolePunch.To = peerId;
           
            var a = protoClient.SendMessageAndWaitResponse(requerstHolePunch, timeOut).Result;
            if (a.Header != MessageEnvelope.RequestTimeout)
            {
                Console.WriteLine("############### Successs #################");
                return true;
            }
            else
            {
                Console.WriteLine("~~~~~~~~~~~~~~~~ Fail ~~~~~~~~~~~");
                return false;
            }
        }

        public async Task<bool> RequestHolePunchAsync(Guid peerId,int timeOut)
        {
            var requerstHolePunch = new MessageEnvelope();
            requerstHolePunch.Header = RelayMessageResources.HolePunchRegister;
            requerstHolePunch.From = sessionId;
            requerstHolePunch.To = peerId;

            var a = await protoClient.SendMessageAndWaitResponse(requerstHolePunch, timeOut);
            if (a.Header != MessageEnvelope.RequestTimeout)
            {
                Console.WriteLine("############### Successs #################");
                return true;
            }
            else
            {
                Console.WriteLine("~~~~~~~~~~~~~~~~ Fail ~~~~~~~~~~~");
                return false;
            }
        }

        #region Send
        public void SendUpMesssage<T>(Guid toId, T message) where T : class
        {
            MessageEnvelope env =  new MessageEnvelope();
            env.From = sessionId;
            env.To = toId;
            env.Header = typeof(T).Name;

            udpClient.SendAsyncMessage(env, message);
        }

        public void SendUpMesssage(Guid toId, MessageEnvelope message)
        {
            message.From = sessionId;
            message.To = toId;
            udpClient.SendAsyncMessage(message);
        }
        public void SendUpMesssage(Guid toId, byte[] data,int offset, int count,string dataName )
        {
            if (!Peers.Contains(toId))
                return;

            MessageEnvelope env = new MessageEnvelope();
            env.From = sessionId;
            env.To = toId;
            env.Header = dataName;

           
            udpClient.SendAsyncMessage(env,data,offset,count);
        }

        public void SendUpMesssage(Guid toId, byte[] data, string dataName)
        {
            if (!Peers.Contains(toId))
                return;

            var envelopedMessage = RelayMessageResources.MakeRelaySatusMessage(sessionId, toId, null);
            envelopedMessage.Header = dataName;

            udpClient.SendAsyncMessage(envelopedMessage, data, 0, data.Length);
        }
        public void SendAsyncMessage(Guid toId, MessageEnvelope message)
        {
            if (!Peers.Contains(toId))
                return;
            message.From = sessionId;
            message.To = toId;
            protoClient.SendAsyncMessage(message);
        }
        public void SendAsyncMessage<T>(Guid toId, T message) where T : class
        {
            if (!Peers.Contains(toId))
                return;
            var envelopedMessage = RelayMessageResources.MakeRelaySatusMessage(sessionId, toId, null);
            envelopedMessage.Header = typeof(T).Name;
            protoClient.SendAsyncMessage(envelopedMessage, message);
        }
        public void SendAsyncMessage(Guid toId, byte[] data, string dataName)
        {
            var envelopedMessage = RelayMessageResources.MakeRelaySatusMessage(sessionId, toId, null);
            envelopedMessage.Header = dataName;
            protoClient.SendAsyncMessage(envelopedMessage, data, 0, data.Length);
        }

        public void SendAsyncMessage(Guid toId, byte[] data,int offset,int count, string dataName)
        {
            var envelopedMessage = RelayMessageResources.MakeRelaySatusMessage(sessionId, toId, null);
            envelopedMessage.Header = dataName;
            protoClient.SendAsyncMessage(envelopedMessage, data, offset, count);
        }

        public async Task<MessageEnvelope> SendRequestAndWaitResponse<T>(Guid toId, T message, int timeoutMs) where T : class
        {
            var envelopedMessage = RelayMessageResources.MakeRelayRequestMessage(Guid.NewGuid(), sessionId, toId, null);
            envelopedMessage.Header = typeof(T).Name;

            var result = await protoClient.SendMessageAndWaitResponse(envelopedMessage, message, timeoutMs);
            return result;
        }

        public async Task<MessageEnvelope> SendRequestAndWaitResponse(Guid toId, byte[] data, string dataName, int timeoutMs)
        {
            var envelopedMessage = RelayMessageResources.MakeRelayRequestMessage(Guid.NewGuid(), sessionId, toId, null);
            envelopedMessage.Header = dataName;

            var response = await protoClient.SendMessageAndWaitResponse(envelopedMessage, data, 0, data.Length, timeoutMs);
            return response;
        }
        public async Task<MessageEnvelope> SendRequestAndWaitResponse(Guid toId, MessageEnvelope message, int timeoutMs)
        {
            message.From = sessionId;
            message.To = toId;

            var response = await protoClient.SendMessageAndWaitResponse(message, timeoutMs);
            return response;
        }

        
        #endregion

        private void StartPunching(MessageEnvelope message)
        {
            Console.WriteLine("Puncing requested");

            string from = message.From.ToString();
            var udpClient = udpClients[message.From];

            string Ip = message.KeyValuePairs.Keys.First();
            int port = int.Parse(message.KeyValuePairs[Ip]);

            udpClient.SetRemoteEnd(Ip,port);
           
            udpRec = new TaskCompletionSource<bool>();
            Task.Run(async () =>
            {
                await Task.WhenAny(udpRec.Task, Task.Delay(1000));
                if (udpRec.Task.IsCompleted && udpRec.Task.Result == true)
                {
                    protoClient.SendAsyncMessage(message);
                }
                else
                {
                    udpClients.TryRemove(Guid.Parse(from), out _);
                }
                
            });

            Console.WriteLine("Puncing");
            message.From = sessionId;
            for (int i = 0; i < 100; i++)
            {
                udpClients[Guid.Parse(from)].SendAsyncMessage(message);

            }

        }

        private void RegisterYourEndpointToServer(MessageEnvelope message)
        {
            Guid from = message.From;
            message.Header = RelayMessageResources.SendFirstMsgHolePunch;
            message.From =sessionId;
            message.Payload = null;

            Console.WriteLine("Sending firstmessage message");

            udpClients[from].SendAsyncMessage(message);

            protoClient.SendAsyncMessage(message);
        }

        private void CreateHolePunchChannel(MessageEnvelope message)
        {
            var udpClient = new EncryptedUdpProtoClient(new ConcurrentAesAlgorithm(message.Payload,message.Payload));
            udpClient.SocketSendBufferSize = 1280000;
            udpClient.ReceiveBufferSize = 12800000;
            udpClient.OnMessageReceived += OnTempReceived;
            udpClient.Bind();
            udpClient.SetRemoteEnd(connectHost, connectPort);
            udpClients[message.From] = udpClient;

            message.Header = RelayMessageResources.SendFirstMsgHolePunch;
            message.From = sessionId;
            message.Payload = null;

            var bytes = serialiser.SerialiseEnvelopedMessage(message);
            Console.WriteLine("Sending channelCreation message");
            udpClient.SendAsync(bytes);

            protoClient.SendAsyncMessage( message);

        }

        private void OnTempReceived(MessageEnvelope obj)
        {
            // unsub, return info, sub to regular channel.
            //obj.Payload = null;
            //tcpClient.SendResponseMessage(obj.MessageId, obj);
            if (!udpRec.Task.IsCompleted)
                udpRec.SetResult(true);

        }

        private void FinalizeUdpInit(MessageEnvelope message)
        {

            ConcurrentAesAlgorithm algo = new ConcurrentAesAlgorithm(message.Payload, message.Payload);
            udpClient.SwapAlgorith(algo);
            protoClient.SendAsyncMessage(message);
        }

        private void ResendUdpInitMessage(MessageEnvelope message)
        {
            message.Header = RelayMessageResources.UdpInit;
            message.From = message.To;
            message.Payload = null;

            udpClient.SendAsyncMessage(message);

            protoClient.SendAsyncMessage(message);
        }

        private void CreateUdpChannel(MessageEnvelope message)
        {
            ConcurrentAesAlgorithm algo = new ConcurrentAesAlgorithm(message.Payload, message.Payload);
            udpClient = new EncryptedUdpProtoClient(algo);
            udpClient.SocketSendBufferSize = 1280000;
            udpClient.ReceiveBufferSize = 12800000;
            udpClient.OnMessageReceived += OnUdpBytesReceived;
            udpClient.Bind();
            udpClient.SetRemoteEnd(connectHost, connectPort);

            message.From = message.To;
            message.Payload = null;

            var bytes = serialiser.SerialiseEnvelopedMessage(message);
            udpClient.SendAsync(bytes);

            protoClient.SendAsyncMessage(message);
            Console.WriteLine("Created channel responding..");

        }

        private void OnUdpBytesReceived(MessageEnvelope message)
        {
            OnUdpMessageReceived?.Invoke(message);
        }

        private void HandleStatusMessageReceived(MessageEnvelope message)
        {
            //if (Peers.Contains(message.From))

            switch (message.Header)
            {
                case (RelayMessageResources.NotifyPeerListUpdate):
                    UpdatePeerList(message);
                    break;

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

                case RelayMessageResources.SendFirstMsgHolePunch:
                    RegisterYourEndpointToServer(message);
                    break;

                case RelayMessageResources.EndpointTransfer:
                    StartPunching(message);

                    break;

               
                default:
                    OnMessageReceived?.Invoke(message);
                    break;
            }
 
        }

        private void UpdatePeerList(MessageEnvelope message)
        {
            if(message.Payload== null)
                return;
            PeerList<string> values =  serialiser.Deserialize<PeerList<string>>(message.Payload, 0, message.Payload.Length);
            if (values.PeerIds == null || values.PeerIds.Count == 0)
                Peers = new HashSet<Guid>();
            else
            {
                HashSet<Guid> serverSet = new HashSet<Guid>(values.PeerIds.Keys);

                foreach (var peer in Peers)
                {
                    if (!serverSet.Contains(peer))
                    {
                        //Peers.Remove(peer);
                        OnPeerUnregistered?.Invoke(peer);
                    }
                }

                foreach (var peer in serverSet)
                {
                    if (!Peers.Contains(peer))
                    {
                        //Peers.Add(peer);
                        OnPeerRegistered?.Invoke(peer);

                    }
                }
                Peers.UnionWith(serverSet);
                Peers.RemoveWhere(x => !serverSet.Contains(x));
            }

            
          
        }

    }
}
