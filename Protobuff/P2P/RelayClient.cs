using NetworkLibrary.Components;
using NetworkLibrary.Components.Statistics;
using NetworkLibrary;
using NetworkLibrary.MessageProtocol.Serialization;
using NetworkLibrary.Utils;
using Protobuff.Components;
using Protobuff.P2P.HolePunch;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Net;
using NetworkLibrary.UDP;
using System.Net.Sockets;
using NetworkLibrary.MessageProtocol;
using System.Threading;
using Protobuff.P2P.StateManagemet;
using System.Diagnostics;
using System.Runtime.InteropServices.ComTypes;
using Protobuff.P2P.Modules;

namespace Protobuff.P2P
{
    public class RelayClient : RelayClientBase
    {
        //public class PeerInfo_
        //{
        //    public string IP;
        //    public int Port;
        //    public IPAddress IPAddress;
        //    public PeerInfo_(PeerInfo info)
        //    {
        //        IPAddress = new IPAddress(info.Address);
        //        IP = IPAddress.ToString();
        //        Port = info.Port;
        //    }
        //    public PeerInfo_()
        //    {

        //    }
        //}

        //public Action<Guid> OnPeerRegistered;
        //public Action<Guid> OnPeerUnregistered;
        //public Action<MessageEnvelope> OnUdpMessageReceived;
        //public Action<MessageEnvelope> OnMessageReceived;
        //public Action OnDisconnected;
        //public RemoteCertificateValidationCallback RemoteCertificateValidationCallback => protoClient.RemoteCertificateValidationCallback;
        //public Guid sessionId { get; private set; }
        //public ConcurrentDictionary<Guid, bool> Peers = new ConcurrentDictionary<Guid, bool>();
        //public bool IsConnected { get => isConnected; private set => isConnected = value; }

        //internal ConcurrentDictionary<Guid, PeerInfo_> PeerInfos { get; private set; } = new ConcurrentDictionary<Guid, PeerInfo_>();

        //internal string connectHost;
        //internal int connectPort;
        //private bool connecting;

        //private SecureProtoMessageClient protoClient;
        //private PingHandler pinger = new PingHandler();
        //private ConcurrentProtoSerialiser serialiser = new ConcurrentProtoSerialiser();
        //private ConcurrentDictionary<Guid, EncryptedUdpProtoClient> directUdpClients = new ConcurrentDictionary<Guid, EncryptedUdpProtoClient>();
        //private ConcurrentDictionary<Guid, IPEndPoint> punchedEndpoints = new ConcurrentDictionary<Guid, IPEndPoint>();
        //private ConcurrentDictionary<Guid, IPEndPoint> pendingHolePunches = new ConcurrentDictionary<Guid, IPEndPoint>();
        //private ConcurrentDictionary<IPEndPoint, ConcurrentAesAlgorithm> punchedEndpointsReverse = new ConcurrentDictionary<IPEndPoint, ConcurrentAesAlgorithm>();
        //private ConcurrentDictionary<IPEndPoint, ConcurrentAesAlgorithm> peerCryptos = new ConcurrentDictionary<IPEndPoint, ConcurrentAesAlgorithm>();

        //private object registeryLocker = new object();
        //private bool isConnected;

        //private TaskCompletionSource<MessageEnvelope> ServerUdpInitCommand =
        //          new TaskCompletionSource<MessageEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        //private TaskCompletionSource<MessageEnvelope> ServerFinalization =
        //           new TaskCompletionSource<MessageEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);

        //private ConcurrentAesAlgorithm udpEncriptor;
        //internal ClientUdpModule udpServer;
        //private IPEndPoint relayServerEndpoint;
        //internal List<EndpointData> localEndpoints;
        //class RegisteryCompetion
        //{
        //    public Guid SessionId;
        //    public ConcurrentAesAlgorithm encryptor;
        //}
        ////SimpleHolepunchstateManager sm2;
        //ClientStateManager clientStateManager;
        //public RelayClient(X509Certificate2 clientCert)
        //{
        //    clientStateManager = new ClientStateManager(this);
        //    protoClient = new SecureProtoMessageClient(clientCert);
        //    protoClient.OnMessageReceived += HandleMessageReceived;
        //    protoClient.OnDisconnected += HandleDisconnect;

        //    udpServer = new ClientUdpModule(0);
        //    udpServer.SocketReceiveBufferSize = 12800000;
        //    udpServer.SocketSendBufferSize = 12800000;
        //    udpServer.OnClientAccepted += UdpClientAccepted;
        //    udpServer.OnBytesRecieved += HandleUdpBytesReceived;
        //    udpServer.StartServer(); 
        //  //  sm2 = new SimpleHolepunchstateManager(this);
        //}

        //private void HandleUdpBytesReceived(IPEndPoint adress, byte[] bytes, int offset, int count)
        //{
        //    count--;
        //    offset++;
        //    if (!IsConnected)
        //        return;
        //    if (adress.Equals(relayServerEndpoint))
        //    {
        //        try
        //        {
        //            var buffer = BufferPool.RentBuffer(count + 256);
        //            int amountDecrypted = udpEncriptor.DecryptInto(bytes, offset, count, buffer, 0);
        //            HandleUdpMessageReceived(serialiser.DeserialiseEnvelopedMessage(buffer, 0, amountDecrypted));
        //            BufferPool.ReturnBuffer(buffer);
        //        }
        //        catch (Exception e)
        //        {
        //            MiniLogger.Log(MiniLogger.LogLevel.Error, "Failed to deserialise envelope message " + e.Message);

        //        }
        //    }
        //    else if (punchedEndpointsReverse.TryGetValue(adress,out var cryptoAlg))
        //    {
        //        if (cryptoAlg != null)
        //        {
        //            var buffer = BufferPool.RentBuffer(count + 256);
        //            int amount = cryptoAlg.DecryptInto(bytes, offset, count, buffer, 0);
        //            var msg = serialiser.DeserialiseEnvelopedMessage(buffer, 0, amount);
        //            HandleUdpMessageReceived(msg);
        //            BufferPool.ReturnBuffer(buffer);

        //        }
        //        else
        //        {
        //            var msg = serialiser.DeserialiseEnvelopedMessage(bytes, offset, count);
        //            HandleUdpMessageReceived(msg);
        //        }
        //    }
        //    // is unknown
        //    else if (peerCryptos.TryGetValue(adress, out var crypto)/*!punchedEndpointsReverse.ContainsKey(adress)*/)
        //    {
        //        try
        //        {
        //            if(crypto == null)
        //            {
        //                var msg = serialiser.DeserialiseEnvelopedMessage(bytes, offset, count);
        //                if (!clientStateManager.HandleMessage(adress, msg))
        //                {
        //                    HandleUdpMessageReceived(msg);
        //                }
        //            }
        //            else
        //            {
        //                var buffer =  BufferPool.RentBuffer(count + 256);
        //                int amount = crypto.DecryptInto(bytes, offset, count, buffer, 0);
        //                var msg = serialiser.DeserialiseEnvelopedMessage(buffer, 0, amount);
        //                if (!clientStateManager.HandleMessage(adress,msg ))
        //                {
        //                    HandleUdpMessageReceived(msg);
        //                }
        //                BufferPool.ReturnBuffer(buffer);

        //            }
        //        }
        //        catch (Exception e)
        //        {
        //            MiniLogger.Log(MiniLogger.LogLevel.Error, "Relay Client Failed to deserialise envelope message " + e.Message);

        //        }
        //    }
        //    //else
        //    //{
        //    //    try
        //    //    {
        //    //        var msg = serialiser.DeserialiseEnvelopedMessage(bytes, offset, count);
        //    //        HandleUdpMessageReceived(msg);

        //    //    }
        //    //    catch (Exception e)
        //    //    {
        //    //        MiniLogger.Log(MiniLogger.LogLevel.Error, "Relay Clinet Failed to deserialise envelope message2 " + e.StackTrace);

        //    //    }

        //    //}
        //}

        //private void UdpClientAccepted(SocketAsyncEventArgs ClientSocket)
        //{

        //}
        //public void TestHP(Guid peerId, int timeOut, bool encrypted = true)
        //{
        //    var state = clientStateManager.CreateHolePncState2(peerId, Guid.NewGuid());
        //    state.Completed += (s) => 
        //    { 
        //        if (state.Status == StateStatus.Completed)
        //        {
        //            HandleCompletedHolepunchState2(state);
        //        }
        //    };
        //}
        //#region Connect & Disconnect

        //public async Task<bool> ConnectAsync(string host, int port)
        //{
        //    if (connecting || IsConnected) return false;

        //    connectHost = host;
        //    connectPort = port;
        //    connecting = true;
        //    try
        //    {
        //        relayServerEndpoint = new IPEndPoint(IPAddress.Parse(connectHost), connectPort);
        //        ServerUdpInitCommand = new TaskCompletionSource<MessageEnvelope>();


        //        await protoClient.ConnectAsync(host, port);

        //        //protoClient.Connect(host, port);
        //        Console.WriteLine("Connected");

        //        var result = await RegisterRoutine2().ConfigureAwait(false);
        //        if (result == null) throw new Exception("routine failed");

        //        sessionId = result.SessionId;
        //        udpEncriptor = result.encryptor;

        //        Volatile.Write(ref isConnected, true);
        //        pinger.PeerRegistered(sessionId);
        //        return true;

        //    }
        //    catch { throw; }
        //    finally
        //    {
        //        connecting = false;
        //    }
        //}


        //private async Task<RegisteryCompetion> RegisterRoutine2()
        //{
        //    Stopwatch sw = new Stopwatch();
        //    sw.Start();
        //    // 0.Send a Reguster message to server.
        //    protoClient.SendAsyncMessage(new MessageEnvelope()
        //    {
        //        IsInternal = true,
        //        Header = Constants.Register,
        //    });
        //    // 1. Wait for server to give you info for your udp message.
        //    if (await Task.WhenAny(ServerUdpInitCommand.Task, Task.Delay(8000)) != ServerUdpInitCommand.Task)
        //    {
        //        return null;
        //    }

        //    ServerFinalization = new TaskCompletionSource<MessageEnvelope>();
        //    //2. obtain server Aes key and send a Udp message and register your endpoint.
        //    var message = ServerUdpInitCommand.Task.Result;

        //    var udpEncriptor = new ConcurrentAesAlgorithm(message.Payload, message.Payload);
        //    MessageEnvelope udpRegistrationMsg = new MessageEnvelope()
        //    {
        //        Header = Constants.UdpInit,
        //        MessageId = message.MessageId,
        //    };

        //    EndpointTransferMessage endpoints = new EndpointTransferMessage();
        //    localEndpoints = GetLocalEndpoints();
        //    endpoints.LocalEndpoints = localEndpoints;

        //    byte[] bytes = EncyrptUdipInitMessage(udpRegistrationMsg, endpoints, udpEncriptor);

        //    udpServer.SendBytesToClient(relayServerEndpoint, bytes, 0, bytes.Length);

        //    // 3. Wait for server finalization message, Udp can drop so we do few resends with timeout.
        //    int retry = 0;
        //    while (await Task.WhenAny(Task.Delay(3000), ServerFinalization.Task).ConfigureAwait(false) != ServerFinalization.Task)
        //    {
        //        if (++retry > 3)
        //        {
        //            return null;
        //        }
        //        udpServer.SendBytesToClient(relayServerEndpoint, bytes, 0, bytes.Length);
        //    }

        //    var finalMSg = ServerFinalization.Task.Result;
        //    // 4.  Client Finalization is send to make server to register us.
        //    protoClient.SendAsyncMessage(new MessageEnvelope()
        //    {
        //        IsInternal = true,
        //        Header = Constants.ClientFinalizationAck,
        //        MessageId = message.MessageId
        //    });

        //    return new RegisteryCompetion()
        //    {
        //        SessionId = message.To,
        //        encryptor = new ConcurrentAesAlgorithm(finalMSg.Payload, finalMSg.Payload),
        //    };
        //}
        //private byte[] EncyrptUdipInitMessage(MessageEnvelope udpRegistrationMsg, EndpointTransferMessage endpoints, ConcurrentAesAlgorithm udpEncriptor)
        //{
        //    var streamTemp = SharerdMemoryStreamPool.RentStreamStatic();
        //    streamTemp.WriteByte(0);

        //    serialiser.EnvelopeMessageWithInnerMessage(streamTemp, udpRegistrationMsg,
        //        (stream) => KnownTypeSerializer.SerializeEndpointTransferMessage(stream, endpoints));
        //    var buff = BufferPool.RentBuffer(512);

        //    var amount = udpEncriptor.EncryptInto(streamTemp.GetBuffer(), 1, streamTemp.Position32,buff,1);
        //    SharerdMemoryStreamPool.ReturnStreamStatic(streamTemp);
        //    var ret = ByteCopy.ToArray(buff, 0, amount + 1);
        //    BufferPool.ReturnBuffer(buff);
        //    return ret;
        //}

        //private List<EndpointData> GetLocalEndpoints()
        //{
        //    List<EndpointData> endpoints = new List<EndpointData>();
        //    var lep = (IPEndPoint)udpServer.LocalEndpoint;

        //    var host = Dns.GetHostEntry(Dns.GetHostName());
        //    foreach (var ip in host.AddressList)
        //    {
        //        if (ip.AddressFamily == AddressFamily.InterNetwork)
        //        {
        //            if (ip.ToString() == "0.0.0.0")
        //                continue;
        //            endpoints.Add(new EndpointData()
        //            {
        //                Ip = ip.GetAddressBytes(),
        //                Port = lep.Port
        //            });
        //        }
        //    }
        //    return endpoints;
        //}


        //[MethodImpl(MethodImplOptions.NoInlining)]
        //public void Connect(string host, int port)
        //{
        //    var res = ConnectAsync(host, port).Result;
        //}

        //[MethodImpl(MethodImplOptions.NoInlining)]
        //public void Disconnect()
        //{
        //    protoClient?.Disconnect();
        //    foreach (var item in directUdpClients)
        //    {
        //        item.Value.Dispose();
        //    }
        //}

        //private void HandleDisconnect()
        //{
        //    lock (registeryLocker)
        //    {
        //        foreach (var peer in PeerInfos)
        //        {
        //            OnPeerUnregistered?.Invoke(peer.Key);
        //        }
        //        PeerInfos = new ConcurrentDictionary<Guid, PeerInfo_>();
        //        Peers.Clear();
        //        directUdpClients.Clear();
        //        OnDisconnected?.Invoke();
        //        IsConnected = false;
        //    }

        //}

        //#endregion

        //#region Ping

        //public void StartPingService(int intervalMs = 1000)
        //{
        //    Task.Run(() => SendPing(intervalMs));
        //}

        //private async void SendPing(int intervalMs)
        //{
        //    while (true)
        //    {
        //        await Task.Delay(intervalMs / 2).ConfigureAwait(false);

        //        MessageEnvelope msg = new MessageEnvelope();
        //        msg.Header = Constants.Ping;
        //        if (IsConnected)
        //        {
        //            msg.TimeStamp = DateTime.Now;
        //            msg.From = sessionId;
        //            msg.To = sessionId;

        //            protoClient.SendAsyncMessage(msg);
        //            SendUdpMesssage(sessionId, msg);
        //            pinger.NotifyTcpPingSent(sessionId, msg.TimeStamp);
        //            pinger.NotifyUdpPingSent(sessionId, msg.TimeStamp);

        //            await Task.Delay(intervalMs / 2).ConfigureAwait(false);
        //            foreach (var peer in Peers.Keys)
        //            {
        //                msg.TimeStamp = DateTime.Now;
        //                SendAsyncMessage(peer, msg);
        //                pinger.NotifyTcpPingSent(peer, msg.TimeStamp);


        //                msg.TimeStamp = DateTime.Now;
        //                SendUdpMesssage(peer, msg);
        //                pinger.NotifyUdpPingSent(peer, msg.TimeStamp);

        //            }
        //        }
        //    }

        //}

        //private void HandlePing(MessageEnvelope message, bool isTcp = true)
        //{
        //    // self ping - server roundtrip.
        //    if (message.From == sessionId)
        //    {
        //        //HandlePong(message,isTcp);
        //        if (isTcp) pinger.HandleTcpPongMessage(message);
        //        else pinger.HandleUdpPongMessage(message);
        //    }
        //    else
        //    {
        //        message.Header = Constants.Pong;
        //        if (isTcp)
        //        {
        //            message.To = message.From;
        //            message.From = sessionId;
        //            protoClient.SendAsyncMessage(message);
        //        }
        //        else
        //            SendUdpMesssage(message.From, message);
        //    }


        //}

        //private void HandlePong(MessageEnvelope message, bool isTcp = true)
        //{
        //    if (isTcp) pinger.HandleTcpPongMessage(message);
        //    else pinger.HandleUdpPongMessage(message);
        //}

        //public Dictionary<Guid, double> GetTcpPingStatus()
        //{
        //    return pinger.GetTcpLatencies();
        //}
        //public Dictionary<Guid, double> GetUdpPingStatus()
        //{
        //    return pinger.GetUdpLatencies();
        //}

        //#endregion Ping

        //#region Send
        //public void SendUdpMesssage<T>(Guid toId, T message, string messageHeader = null, int channel = 0) where T : IProtoMessage
        //{
        //    if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
        //        return;
        //    MessageEnvelope env = new MessageEnvelope();
        //    env.From = sessionId;
        //    env.To = toId;
        //    env.Header = messageHeader == null ? typeof(T).Name : messageHeader;

        //    if (directUdpClients.TryGetValue(toId, out var client))
        //    {
        //        client.SendAsyncMessage(env, message);
        //    }
        //    else if (punchedEndpoints.TryGetValue(toId,out var endpoint))
        //    {
        //        udpServer.SendAsync(endpoint, env, message, punchedEndpointsReverse[endpoint]);
        //    }
        //    else
        //        udpServer.SendAsync(relayServerEndpoint, env, message, udpEncriptor);
        //}

        //public void SendUdpMesssage(Guid toId, MessageEnvelope message, int channel = 0)
        //{
        //    if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
        //        return;
        //    message.From = sessionId;
        //    message.To = toId;

        //    if (directUdpClients.TryGetValue(toId, out var client))
        //    {
        //        client.SendAsyncMessage(message);
        //    }
        //    else if (punchedEndpoints.TryGetValue(toId, out var endpoint))
        //    {
        //        udpServer.SendAsync(endpoint, message, punchedEndpointsReverse[endpoint]);

        //    }
        //    else
        //        udpServer.SendAsync(relayServerEndpoint, message, udpEncriptor);
        //}
        //public void SendUdpMesssage<T>(Guid toId, MessageEnvelope message, T innerMessage, int channel = 0) where T : IProtoMessage
        //{
        //    if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
        //        return;

        //    message.From = sessionId;
        //    message.To = toId;

        //    if (directUdpClients.TryGetValue(toId, out var client))
        //    {
        //        client.SendAsyncMessage(message, innerMessage);
        //    }
        //    else if (punchedEndpoints.TryGetValue(toId, out var endpoint))
        //    {
        //        udpServer.SendAsync(endpoint, message, innerMessage, punchedEndpointsReverse[endpoint]);
        //    }
        //    else
        //        udpServer.SendAsync(relayServerEndpoint, message, innerMessage, udpEncriptor);

        //}
        //public void SendUdpMesssage(Guid toId, byte[] data, int offset, int count, string dataName, int channel = 0)
        //{
        //    if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
        //        return;

        //    MessageEnvelope env = new MessageEnvelope();
        //    env.From = sessionId;
        //    env.To = toId;
        //    env.Header = dataName;
        //    env.SetPayload(data, offset, count);

        //    if (directUdpClients.TryGetValue(toId, out var client))
        //    {
        //        client.SendAsyncMessage(env);
        //    }
        //    else if (punchedEndpoints.TryGetValue(toId, out var endpoint))
        //    {
        //        udpServer.SendAsync(endpoint, env, punchedEndpointsReverse[endpoint]);

        //    }
        //    else
        //        udpServer.SendAsync(relayServerEndpoint, env, udpEncriptor);

        //}

        //public void SendAsyncMessage(Guid toId, MessageEnvelope message)
        //{
        //    if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
        //        return;

        //    message.From = sessionId;
        //    message.To = toId;
        //    protoClient.SendAsyncMessage(message);
        //}
        //public void SendAsyncMessage<T>(Guid toId, T message, string messageHeader = null) where T : IProtoMessage
        //{
        //    if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
        //        return;

        //    var envelope = new MessageEnvelope()
        //    {
        //        From = sessionId,
        //        To = toId,
        //    };

        //    envelope.Header = messageHeader == null ? typeof(T).Name : messageHeader;
        //    protoClient.SendAsyncMessage(envelope, message);
        //}

        //public void SendAsyncMessage<T>(Guid toId, MessageEnvelope envelope, T message) where T : IProtoMessage
        //{
        //    if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
        //        return;

        //    envelope.From = sessionId;
        //    envelope.To = toId;
        //    protoClient.SendAsyncMessage(envelope, message);
        //}

        //public void SendAsyncMessage(Guid toId, MessageEnvelope envelope, Action<PooledMemoryStream> serializationCallback)
        //{
        //    if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
        //        return;

        //    envelope.From = sessionId;
        //    envelope.To = toId;
        //    protoClient.SendAsyncMessage(envelope, serializationCallback);
        //}

        //public void SendAsyncMessage(Guid toId, byte[] data, string dataName)
        //{
        //    if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
        //        return;

        //    var envelope = new MessageEnvelope()
        //    {
        //        From = sessionId,
        //        To = toId,
        //        Header = dataName
        //    };
        //    protoClient.SendAsyncMessage(envelope, data, 0, data.Length);
        //}

        //public void SendAsyncMessage(Guid toId, byte[] data, int offset, int count, string dataName)
        //{
        //    if (!Peers.TryGetValue(toId, out _) && toId != sessionId)
        //        return;

        //    var envelope = new MessageEnvelope()
        //    {
        //        From = sessionId,
        //        To = toId,
        //        Header = dataName
        //    };
        //    protoClient.SendAsyncMessage(envelope, data, offset, count);
        //}

        //public Task<MessageEnvelope> SendRequestAndWaitResponse<T>(Guid toId, T message, string messageHeader = null, int timeoutMs = 10000) where T : IProtoMessage
        //{
        //    var envelope = new MessageEnvelope()
        //    {
        //        From = sessionId,
        //        To = toId,
        //        MessageId = Guid.NewGuid(),
        //        Header = messageHeader == null ? typeof(T).Name : messageHeader
        //    };

        //    var task = protoClient.SendMessageAndWaitResponse(envelope, message, timeoutMs);
        //    return task;
        //}

        //public Task<MessageEnvelope> SendRequestAndWaitResponse(Guid toId, byte[] data, string dataName, int timeoutMs = 10000)
        //{
        //    var envelope = new MessageEnvelope()
        //    {
        //        From = sessionId,
        //        To = toId,
        //        MessageId = Guid.NewGuid(),
        //        Header = dataName
        //    };

        //    var response = protoClient.SendMessageAndWaitResponse(envelope, data, 0, data.Length, timeoutMs);
        //    return response;
        //}
        //public Task<MessageEnvelope> SendRequestAndWaitResponse(Guid toId, MessageEnvelope message, int timeoutMs = 10000)
        //{
        //    message.From = sessionId;
        //    message.To = toId;

        //    var response = protoClient.SendMessageAndWaitResponse(message, timeoutMs);
        //    return response;
        //}
        //public Task<MessageEnvelope> SendRequestAndWaitResponse(Guid toId, MessageEnvelope message, byte[] buffer, int offset, int count, int timeoutMs = 10000)
        //{
        //    message.From = sessionId;
        //    message.To = toId;

        //    var response = protoClient.SendMessageAndWaitResponse(message, buffer, offset, count, timeoutMs);
        //    return response;
        //}

        //public Task<MessageEnvelope> SendRequestAndWaitResponse<T>(Guid toId, MessageEnvelope envelope, T message, int timeoutMs = 10000) where T : IProtoMessage
        //{
        //    envelope.From = sessionId;
        //    envelope.To = toId;

        //    var response = protoClient.SendMessageAndWaitResponse(envelope, message, timeoutMs);
        //    return response;
        //}

        //#endregion
        //private void HandleMessageReceived(MessageEnvelope message)
        //{

        //    if (message.IsInternal)
        //    {
        //        if (clientStateManager.HandleMessage(message))
        //            return;

        //        switch (message.Header)
        //        {
        //            case Constants.ServerCmd:
        //                message.LockBytes();
        //                ServerUdpInitCommand.TrySetResult(message);
        //                return;

        //            case Constants.ServerFinalizationCmd:
        //                message.LockBytes();
        //                ServerFinalization.TrySetResult(message);
        //                return;

        //            case (Constants.NotifyPeerListUpdate):
        //                UpdatePeerList(message);
        //                break;

        //            default:
        //                OnMessageReceived?.Invoke(message);
        //                break;
        //        }

        //    }
        //    else
        //    {
        //        switch (message.Header)
        //        {
        //            case Constants.Ping:
        //                HandlePing(message);
        //                break;

        //            case Constants.Pong:
        //                HandlePong(message);
        //                break;

        //            default:
        //                OnMessageReceived?.Invoke(message);
        //                break;

        //        }

        //    }

        //}

        //#region Hole Punch
        //public bool RequestHolePunch(Guid peerId, int timeOut = 10000, bool encrypted = true)
        //{
        //    return RequestHolePunchAsync(peerId, timeOut, encrypted).Result;
        //}
        //// Ask the server about holepunch
        //public Task<bool> RequestHolePunchAsync(Guid peerId, int timeOut, bool encrypted = true)
        //{
        //    if (clientStateManager.IsHolepunchStatePending(peerId) ||
        //        punchedEndpoints.ContainsKey(peerId) ||
        //        directUdpClients.ContainsKey(peerId))
        //    {
        //        return Task.FromResult(false);
        //    }


        //    var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        //    var state = clientStateManager.CreateHolePncState2(peerId, Guid.NewGuid());
        //    state.Completed += (s) =>
        //    {
        //        if (state.Status == StateStatus.Completed)
        //        {
        //           // HandleCompletedHolepunchState2(state);
        //            tcs.SetResult(true);
        //        }
        //        else
        //        {
        //            var state2 = clientStateManager.CreateHolepunchState(peerId, timeOut, encrypted);
        //            state2.Completed += (st) =>
        //            {
        //                if (st.Status == StateStatus.Completed)
        //                {
        //                    var client = state2.holepunchClient;
        //                    client.OnMessageReceived = null;
        //                    client.OnMessageReceived += HandleUdpMessageReceived;
        //                    directUdpClients.TryAdd(peerId, client);
        //                    tcs.SetResult(true);
        //                }
        //                else
        //                    tcs.SetResult(false);
        //            };

        //        }
        //    };
        //    return tcs.Task;

        //    //TestHP(peerId,timeOut,false);
        //    //var tcs =  new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        //    //var state = clientStateManager.CreateHolepunchState(peerId, timeOut, encrypted);
        //    //state.Completed += (st) =>
        //    //{
        //    //    if(st.Status == StateStatus.Completed)
        //    //    {
        //    //        var client = state.holepunchClient;
        //    //        client.OnMessageReceived = null;
        //    //        client.OnMessageReceived += HandleUdpMessageReceived;
        //    //        directUdpClients.TryAdd(peerId, client);
        //    //        tcs.SetResult(true);
        //    //        return;
        //    //    }
        //    //    tcs.SetResult(false);
        //    //};

        //    //return tcs.Task;

        //}

        //#endregion

        //internal void HandleUdpMessageReceived(MessageEnvelope message)
        //{
        //    if (message.Header == null)
        //    {
        //        return;
        //    }
        //    else if (message.Header.Equals(Constants.HoplePunch)) { }
        //    else if (message.Header.Equals(Constants.Ping)) HandlePing(message, isTcp: false);
        //    else if (message.Header.Equals(Constants.Pong)) HandlePong(message, isTcp: false);

        //    else OnUdpMessageReceived?.Invoke(message);
        //}

        //public PeerInfo_ GetPeerInfo(Guid peerId)
        //{
        //    PeerInfos.TryGetValue(peerId, out var val);
        //    return val;
        //}

        //protected virtual void UpdatePeerList(MessageEnvelope message)
        //{
        //    lock (registeryLocker)
        //    {
        //        PeerList serverPeerInfo = null;
        //        if (message.Payload == null)
        //            serverPeerInfo = new PeerList() { PeerIds = new Dictionary<Guid, PeerInfo>() };
        //        else
        //        {
        //            serverPeerInfo = KnownTypeSerializer.DeserializePeerList(message.Payload, message.PayloadOffset);
        //        }
        //        List<Guid> registered = new List<Guid>();
        //        List<Guid> unregistered = new List<Guid>();
        //        foreach (var peer in Peers.Keys)
        //        {
        //            if (!serverPeerInfo.PeerIds.ContainsKey(peer))
        //            {
        //                Peers.TryRemove(peer, out _);
        //                directUdpClients.TryRemove(peer, out _);
        //                pinger.PeerUnregistered(peer);
        //                PeerInfos.TryRemove(peer, out _);
        //                unregistered.Add(peer);
        //               // ThreadPool.UnsafeQueueUserWorkItem((s) => { OnPeerUnregistered?.Invoke(peer); }, null);
        //            }
        //        }

        //        foreach (var peer in serverPeerInfo.PeerIds.Keys)
        //        {
        //            if (!Peers.TryGetValue(peer, out _))
        //            {
        //                Peers.TryAdd(peer, true);
        //                pinger.PeerRegistered(peer);
        //                PeerInfos.TryAdd(peer, new PeerInfo_(serverPeerInfo.PeerIds[peer]));
        //                registered.Add(peer);
        //               // ThreadPool.UnsafeQueueUserWorkItem((s) => { OnPeerRegistered?.Invoke(peer); }, null);
        //            }
        //        }

        //        ThreadPool.UnsafeQueueUserWorkItem((s) =>
        //        {
        //            foreach (var peer in unregistered)
        //            {
        //                OnPeerUnregistered?.Invoke(peer);
        //            }
        //            foreach (var peer in registered)
        //            {
        //                OnPeerRegistered?.Invoke(peer);
        //            }
        //        }, null);

        //    }
        //}

        //public void GetTcpStatistics(out TcpStatistics stats) => protoClient.GetStatistics(out stats);

        //internal void HandleCompletedHolepunchState(ClientHolepunchState state)
        //{
        //    var client = state.holepunchClient;
        //    client.OnMessageReceived = null;
        //    client.OnMessageReceived += HandleUdpMessageReceived;
        //    directUdpClients.TryAdd(state.DestinationId, client);
        //}

        //internal void HandleCompletedHolepunchState2(SimpleClientHPState state)
        //{
        //    punchedEndpointsReverse.TryAdd(state.succesfulEp, peerCryptos[state.succesfulEp]);
        //    punchedEndpoints.TryAdd(state.destinationId, state.succesfulEp);//

        //    MiniLogger.Log(MiniLogger.LogLevel.Info, $"Punched on {state.succesfulEp}");
        //}

        //internal void RegisterCrypto(byte[] key, List<EndpointData> associatedEndpoints)
        //{
        //    ConcurrentAesAlgorithm crypto = null;
        //    if (key != null)
        //        crypto = new ConcurrentAesAlgorithm(key, key);
        //    foreach (var item in associatedEndpoints)
        //    {
        //        peerCryptos.TryAdd(item.ToIpEndpoint(), crypto);
        //    }
        //}


        public RelayClient(X509Certificate2 clientCert) : base(clientCert)
        {
        }

      
    }
}
