using NetworkLibrary.Components.Crypto.DiffieHellman;
using NetworkLibrary.DistributedP2P.Components;
using NetworkLibrary.P2P.Components.HolePunch;
using NetworkLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkLibrary.DistributedP2P.Client.StateManagement
{
    class ClientHolepunchData
    {
        public ChannelInfo ChannelInfo { get; set; }
        public EndpointTransferMessage Endpoints { get; set; }
        public byte[] DHPublic;
    }

    internal class ClientSequentialTcpHolepunchState : ConversationStateBase
    {
        private readonly Guid destId;
        private readonly IDistributedConnection connection;
        private readonly EndpointData serverEndpoint;
        private readonly EndpointData discoveryServerEp;
        private bool isInitiator;
        public Socket Socket;
        public IPEndPoint SuccesfulEndpoint;

        private DiffieHellman df = new DiffieHellman();
        private byte[] othersPublicKey;
        public byte[] SharedSecret;
        public ChannelInfo ChannelInfo;

        private Socket listeningSocket;
        private Socket acceptedSocket;
        private Socket connectedSocket;
        private int established = 0;
        private int connected = 0;
        private int accepted = 0;

        int swapCnt = 0;

        bool isListening = false;
        List<EndpointData> localEndpoints = new List<EndpointData>();

        private IPEndPoint selfRemoteEp;
        private IPEndPoint selfLocalEp = new IPEndPoint(IPAddress.Any, 0);
        private EndpointData publicEndpointToConnect;

        int conditionCount = 0;


        private bool IsEstablished => Interlocked.CompareExchange(ref established, 0, 0) == 1;
        public ClientSequentialTcpHolepunchState(Guid stateId, Guid destId, IDistributedConnection connection, EndpointData serverEndpoint, EndpointData discoveryServerEp, ChannelInfo info) : base(stateId, 10000)
        {
            this.destId = destId;
            this.connection = connection;
            this.serverEndpoint = serverEndpoint;
            this.discoveryServerEp = discoveryServerEp;
            this.ChannelInfo = info;
        }

        //the initiator
        public async void Start()
        {
            isInitiator = true;
            Log(StateId.ToString());

            await BindPort();
            Log($"Local port {selfLocalEp.Port} Remote port {selfRemoteEp.Port}");

            var msg = CreateEnvelope();
            msg.To = destId;
            msg.Header = InternalConstants.RequestSequentialHolepunchTcp;

            var stream = SharerdMemoryStreamPool.RentStreamStatic();
            KnownTypeSerializer.SerializeHolepunchData(stream, GetHpData());

            msg.SetPayload(stream.GetBuffer(), 0, stream.Position32);
            connection.SendAsyncMessage(msg);

            SharerdMemoryStreamPool.ReturnStreamStatic(stream);
        }



        public override void HandleMessage(MessageEnvelope message)
        {
            switch (message.Header)
            {
                case InternalConstants.RequestSequentialHolepunchTcp:
                    HandleRemoteHpRequest(message);
                    break;

                case InternalConstants.StartHP:
                    message.LockBytes();
                    ThreadPool.UnsafeQueueUserWorkItem((s) => StartHolepunchRoutine(message), null);
                    break;

                case InternalConstants.PunchSuccesAck:
                    HandleRemoteSucces(message);
                    break;
                case InternalConstants.PunchFailAck:
                    HandleFailure();
                    break;
                case InternalConstants.PunchSwap:
                    Swap();
                    break;
            }
        }

        // the destination peer of hp
        private async void HandleRemoteHpRequest(MessageEnvelope message)
        {
            Log(StateId.ToString());
            int offs = message.PayloadOffset;
            var hpData = KnownTypeSerializer.DeserializeHolepunchData(message.Payload, ref offs);

            ChannelInfo = hpData.ChannelInfo;
            await BindPort();
            Log($"Local port {selfLocalEp.Port} Remote port {selfRemoteEp.Port}");

            StartTcpListener();
            Log("listening");

            var msg = CreateEnvelope();
            msg.To = destId;
            msg.Header = InternalConstants.AckRequestHolepunchTcp;

            var hpd = GetHpData();
            hpd.ChannelInfo = null;

            var stream = SharerdMemoryStreamPool.RentStreamStatic();
            KnownTypeSerializer.SerializeHolepunchData(stream, hpd);
            msg.SetPayload(stream.GetBuffer(), 0, stream.Position32);

            connection.SendAsyncMessage(msg);
            SharerdMemoryStreamPool.ReturnStreamStatic(stream);
        }

        private void StartHolepunchRoutine(MessageEnvelope message)
        {
            if (IsCompleted()) return;
            int offs = message.PayloadOffset;

            var hpData = KnownTypeSerializer.DeserializeHolepunchData(message.Payload, ref offs);

            var epMsg = hpData.Endpoints;
            othersPublicKey = hpData.DHPublic;
            localEndpoints = epMsg.LocalEndpoints;

            bool useServerIp = IPHelper.IsZero(epMsg.IpRemote);
            publicEndpointToConnect = new EndpointData() { Ip = useServerIp ? serverEndpoint.Ip : epMsg.IpRemote, Port = epMsg.PortRemote };

            SignalCompletionCondition();
            if (IsCompleted()) return;

            if (!isListening)
            {
                TryPunch();
            }

        }


        private void TryPunch()
        {
            try
            {
                if (localEndpoints.Count > 0)
                {
                    foreach (EndpointData localEp in localEndpoints)
                    {
                        if (TryConnect(localEp, 600))
                            return;

                        if (IsCompleted()) return;

                    }
                }

                for (int i = 0; i < 1; i++)
                {
                    if (TryConnect(publicEndpointToConnect, (2000)))
                        return;

                    if (IsCompleted()) return;

                }
            }
            catch { }
            finally
            {
                if (Interlocked.CompareExchange(ref established, 0, 0) == 0)
                    SwapAndNotify();
            }
        }

        //this one only when we tried to connect and failed
        // so we will listen only
        private void SwapAndNotify()
        {
            if (IsCompleted()) return;


            int cnt = 0;
            while (!isListening)
            {
                try
                {
                    if (IsCompleted()) return;
                    Swap();
                    if (IsCompleted()) return;
                }
                catch
                {
                    Thread.Sleep(200);
                }

                cnt++;
                if (cnt > 10)
                    break;

            }

            var msg = CreateEnvelope();
            msg.Header = InternalConstants.PunchSwap;
            connection.SendAsyncMessage(msg);
        }

        private void Swap()
        {
            if (swapCnt++ > 3)
            {
                Log("Max attempts reached");
                Cancel();
                return;
            }

            if (IsCompleted()) return;

            if (isListening)
            {
                Log("Swapping to Sender");
                StopListener();
                ThreadPool.UnsafeQueueUserWorkItem(_ => TryPunch(), null);
            }
            else
            {
                Log("Swapping to Listener");
                StartTcpListener();
            }
        }

        private bool TryConnect(EndpointData endpoint, int timeoutMs = 600)
        {
            Socket connectSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                Log("Connecting to " + endpoint.ToIpEndpoint().ToString());
                connectSocket.Bind(selfLocalEp);

                var connectTask = connectSocket.ConnectAsync(endpoint.ToIpEndpoint());
                var timeoutTask = Task.Delay(timeoutMs);

                if (Task.WhenAny(connectTask, timeoutTask).GetAwaiter().GetResult() == connectTask)
                {
                    if (connectTask.IsFaulted)
                    {
                        throw connectTask.Exception;
                    }
                    HandleConnectedSocket(connectSocket);
                    Log($"Successfully connected to {endpoint.ToIpEndpoint()}");
                    return true;

                }
                else
                {
                    Log($"Connection to {endpoint.ToIpEndpoint()} timed out after {timeoutMs}ms");
                    connectSocket.Close();
                    connectSocket.Dispose();
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log($"Connect attempt to {endpoint.ToIpEndpoint()} failed: {ex.Message}");
                connectSocket.Close();
                return false;
            }
        }

        private async Task BindPort()
        {
            var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            clientSocket.Bind(selfLocalEp);
            selfLocalEp = (IPEndPoint)clientSocket.LocalEndPoint;

            var remoteEp = await EndpointDiscoveryClient.GetTcpPublicEndpoint(clientSocket, discoveryServerEp.ToIpEndpoint(), 5000);
            if (remoteEp == null)
            {
                Log("Failed to get public endpoint");
                remoteEp = new EndpointData("0.0.0.0", selfLocalEp.Port);
            }

            selfRemoteEp = remoteEp.ToIpEndpoint();

            try
            {
                clientSocket?.Close();
                clientSocket?.Dispose();
                clientSocket = null;
            }
            catch { }

        }

        private int StartTcpListener()
        {
            listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listeningSocket.SendBufferSize = 12800000;
            listeningSocket.ReceiveBufferSize = 12800000;

            listeningSocket.Bind(selfLocalEp);

            selfLocalEp = (IPEndPoint)listeningSocket.LocalEndPoint;


            Listen();
            isListening = true;
            return ((IPEndPoint)listeningSocket.LocalEndPoint).Port;
        }

        private void Listen()
        {
            listeningSocket.Listen(1);
            listeningSocket.BeginAccept(new AsyncCallback(AcceptCallback), listeningSocket);
        }

        private void AcceptCallback(IAsyncResult ar)
        {
            try
            {
                Socket listener = (Socket)ar.AsyncState;
                Socket handler = listener.EndAccept(ar);

                HandleAcceptedSocket(handler);
            }
            catch (Exception e)
            {
                Log("Failed Accept: " + e.Message);
            }

        }

        private void StopListener()
        {
            try
            {
                isListening = false;
                listeningSocket?.Close();
                listeningSocket?.Dispose();
                listeningSocket = null;
            }
            catch { }
        }

        // In this case one or the other

        private void HandleConnectedSocket(Socket socket)
        {
            if (IsCompleted()) return;

            Interlocked.Exchange(ref established, 1);
            if (Interlocked.Exchange(ref connected, 1) == 1)
                return;

            connectedSocket = socket;

            var msg = CreateEnvelope();
            msg.Header = InternalConstants.PunchSucces;
            msg.KeyValuePairs = new Dictionary<string, string>();
            msg.KeyValuePairs["Status"] = "Connected";
            connection.SendAsyncMessage(msg);
        }

        private void HandleAcceptedSocket(Socket socket)
        {
            if (IsCompleted()) return;


            Interlocked.Exchange(ref established, 1);
            if (Interlocked.Exchange(ref accepted, 1) == 1)
                return;

            Log($"Successfully accepted {(IPEndPoint)socket.RemoteEndPoint}");
            acceptedSocket = socket;


            var msg = CreateEnvelope();
            msg.Header = InternalConstants.PunchSucces;
            msg.KeyValuePairs = new Dictionary<string, string>();
            msg.KeyValuePairs["Status"] = "Accepted";
            connection.SendAsyncMessage(msg);
        }


        private void HandleFailure()
        {
            Log("Failed Punch");
            Completed(false);
        }

        private void HandleRemoteSucces(MessageEnvelope message)
        {
            SignalCompletionCondition();
        }
        private void SignalCompletionCondition()
        {
            if (Interlocked.Increment(ref conditionCount) == 2)
            {
                if (ChannelInfo.RequiresKeyExchange())
                    SharedSecret = df.CalculateSharedSecret(othersPublicKey);

                if (connectedSocket != null)
                {
                    Socket = connectedSocket;
                }
                else
                {
                    Socket = acceptedSocket;
                }

                if (Socket == null)
                {
                    Log("Failed to get socket");
                    Completed(false);
                    return;
                }
                SuccesfulEndpoint = (IPEndPoint)Socket.RemoteEndPoint;
                Log("Punched");
                Completed(true);
            }
        }


        public override void Cancel()
        {
            lock (cancellationMutex)
            {
                if (!IsCompleted())
                {
                    Log("Cancelled");
                    var msg = CreateEnvelope();
                    msg.Header = InternalConstants.PunchFail;
                    connection.SendAsyncMessage(msg);
                    Completed(false);
                }
            }

        }

        protected override void Completed(bool succes)
        {
            base.Completed(succes);

            try
            {
                if (!IsSuccesful)
                {
                    acceptedSocket?.Close();
                    acceptedSocket?.Dispose();
                    connectedSocket?.Close();
                    connectedSocket?.Dispose();
                }

                listeningSocket?.Close();
                listeningSocket?.Dispose();
            }
            catch { }

        }

        protected override void Log(string log)
        {
            //return;
            string prefix = isInitiator ? "A: " : "B: ";
            base.Log(prefix + log);
        }

        private ClientHolepunchData GetHpData()
        {
            ClientHolepunchData hpd = new ClientHolepunchData();
            hpd.ChannelInfo = ChannelInfo;

            var epm = new EndpointTransferMessage();
            var pub = new EndpointData(selfRemoteEp);
            epm.IpRemote = pub.Ip;
            epm.PortRemote = pub.Port;

            var localIps = IPHelper.GetLocalIPAddresses4();
            int localPort = selfLocalEp.Port;
            foreach (var ip in localIps)
            {
                epm.LocalEndpoints.Add(new EndpointData(ip, localPort));
            }

            hpd.Endpoints = epm;

            if (ChannelInfo.RequiresKeyExchange())
            {
                hpd.DHPublic = df.GetPublicKey();
            }
            return hpd;
        }
    }


}
