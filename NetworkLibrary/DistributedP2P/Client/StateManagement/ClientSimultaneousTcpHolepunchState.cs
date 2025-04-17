using NetworkLibrary.Components.Crypto.DiffieHellman;
using NetworkLibrary.DistributedP2P.Components;
using NetworkLibrary.P2P.Components.HolePunch;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetworkLibrary.Utils;

namespace NetworkLibrary.DistributedP2P.Client.StateManagement
{
    internal class ClientSimultaneousTcpHolepunchState : ConversationStateBase
    {
        private readonly Guid destId;
        private readonly IDistributedConnection connection;
        private readonly EndpointData serverEndpoint;
        private readonly EndpointData discoveryServerEp;
        private bool isInitiator;
        public Socket Socket;
        public IPEndPoint SuccesfulEndpoint;

        private DiffieHellman df = new DiffieHellman();
        private byte[] otherPublicKey;
        List<EndpointData> localEndpoints = new List<EndpointData>();

        public byte[] SharedSecret;
        public ChannelInfo ChannelInfo;
        private int localPort;
        private Socket listeningSocket;
        private Socket acceptedSocket;
        private Socket connectedSocket;
        private int established = 0;
        private int connected = 0;
        private int accepted = 0;

        private IPEndPoint selfRemoteEp;
        private IPEndPoint selfLocalEp = new IPEndPoint(IPAddress.Any, 0);
        private EndpointData publicEndpointToConnect;


        private bool IsEstablished => Interlocked.CompareExchange(ref established, 0, 0) == 1;
        public ClientSimultaneousTcpHolepunchState(Guid stateId, Guid destId, IDistributedConnection connection, EndpointData serverEndpoint, EndpointData discoveryServerEp, ChannelInfo info, ILogger logger) : base(stateId, 5000, logger)
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
            Log(LogType.Debug, StateId.ToString());

            await BindPort();
            Log(LogType.Debug, $"Local port {selfLocalEp.Port} Remote port {selfRemoteEp.Port}");

            StartListening();
            Log(LogType.Debug, "listening");

            var msg = CreateEnvelope();
            msg.To = destId;
            msg.Header = InternalConstants.RequestSimultaneousHolepunchTcp;

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
                case InternalConstants.RequestSimultaneousHolepunchTcp:
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
            }
        }

        // the destination peer of hp
        private async void HandleRemoteHpRequest(MessageEnvelope message)
        {
            Log(LogType.Debug, StateId.ToString());
            int offs = message.PayloadOffset;
            var hpData = KnownTypeSerializer.DeserializeHolepunchData(message.Payload, ref offs);

            ChannelInfo = hpData.ChannelInfo;
            await BindPort();
            Log(LogType.Debug, $"Local port {selfLocalEp.Port} Remote port {selfRemoteEp.Port}");

            StartListening();
            Log(LogType.Debug, "listening");

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

        private async Task BindPort()
        {
            var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            clientSocket.Bind(selfLocalEp);
            selfLocalEp = (IPEndPoint)clientSocket.LocalEndPoint;

            var remoteEp = await EndpointDiscoveryClient.GetTcpPublicEndpoint(clientSocket, discoveryServerEp.ToIpEndpoint(), 5000);
            if (remoteEp == null)
            {
                Log(LogType.Warning, "Failed to get public endpoint");
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

        private void StartHolepunchRoutine(MessageEnvelope message)
        {
            try
            {


                if (IsCompleted()) return;
                int offs = message.PayloadOffset;

                var hpData = KnownTypeSerializer.DeserializeHolepunchData(message.Payload, ref offs);

                var epMsg = hpData.Endpoints;
                otherPublicKey = hpData.DHPublic;
                localEndpoints = epMsg.LocalEndpoints;

                bool useServerIp = IPHelper.IsZero(epMsg.IpRemote);
                publicEndpointToConnect = new EndpointData() { Ip = useServerIp ? serverEndpoint.Ip : epMsg.IpRemote, Port = epMsg.PortRemote };
                var time = double.Parse(message.KeyValuePairs["Time"]);


                // if there are local endpoints to test
                if (epMsg.LocalEndpoints.Count > 0)
                {
                    foreach (EndpointData localEp in epMsg.LocalEndpoints)
                    {
                        if (TryConnect(localEp, 500))
                            return;
                        if (IsEstablished) return;
                    }
                }

                if (IsEstablished) return;

                var now = connection.GetTime();
                var delay = time - now;

                Log(LogType.Debug, "Delay: " + delay.ToString() + "ms");

                PreciseTimeAwaiter.Wait(delay);
                if (IsEstablished) return;

                for (int i = 0; i < 4; i++)
                {
                    if (TryConnect(publicEndpointToConnect, (2000)))
                        return;
                    if (IsEstablished) return;
                }
            }
            catch (Exception e)
            {
                if (!IsCompleted())
                {
                    Log(LogType.Exception, e.Message + "\n" + e.StackTrace);
                    TimedOut();
                }
            }

        }


        private bool TryConnect(EndpointData endpoint, int timeoutMs = 600)
        {
            Socket connectSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                Log(LogType.Debug, "Connecting to " + endpoint.ToIpEndpoint().ToString());
                connectSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                connectSocket.Bind(new IPEndPoint(IPAddress.Any, localPort));

                var connectTask = connectSocket.ConnectAsync(endpoint.ToIpEndpoint());
                var timeoutTask = Task.Delay(timeoutMs);

                if (Task.WhenAny(connectTask, timeoutTask).GetAwaiter().GetResult() == connectTask)
                {

                    connectSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);
                    HandleConnectedSocket(connectSocket);
                    Log(LogType.Debug, $"Successfully connected to {endpoint.ToIpEndpoint()}");
                    return true;

                }
                else
                {
                    Log(LogType.Debug, $"Connection to {endpoint.ToIpEndpoint()} timed out after {timeoutMs}ms");
                    connectSocket.Close();
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log(LogType.Debug, $"Connect attempt to {endpoint.ToIpEndpoint()} failed: {ex.Message}");
                connectSocket.Close();
                return false;
            }
        }


        private int StartListening()
        {
            listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listeningSocket.SendBufferSize = 12800000;
            listeningSocket.ReceiveBufferSize = 12800000;
            listeningSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            listeningSocket.Bind(selfLocalEp);

            Listen();

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
                Log(LogType.Debug, "Failed Accept: " + e.Message);
            }
         
        }

        private void HandleConnectedSocket(Socket socket)
        {
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
            Interlocked.Exchange(ref established, 1);
            if (Interlocked.Exchange(ref accepted, 1) == 1)
                return;

            Log(LogType.Debug, $"Successfully accepted {(IPEndPoint)socket.RemoteEndPoint}");
            acceptedSocket = socket;
         

            var msg = CreateEnvelope();
            msg.Header = InternalConstants.PunchSucces;
            msg.KeyValuePairs = new Dictionary<string, string>();
            msg.KeyValuePairs["Status"] = "Accepted";
            connection.SendAsyncMessage(msg);
        }

        private void TimedOut()
        {
            Log(LogType.Debug, "Timed out");
            var msg = CreateEnvelope();
            msg.Header = InternalConstants.PunchFail;
            connection.SendAsyncMessage(msg);
            Cancel();
        }

        private void HandleFailure()
        {
            Log(LogType.Debug, "Failed Punch");
            Completed(false);
        }

        private void HandleRemoteSucces(MessageEnvelope message)
        {
            string use = message.KeyValuePairs["Use"];
            if (ChannelInfo.RequiresKeyExchange())
                SharedSecret = df.CalculateSharedSecret(otherPublicKey);

            if (use == "Connected")
            {
                Socket = connectedSocket;
            }
            else
            {
                Socket = acceptedSocket;
            }

            SuccesfulEndpoint = (IPEndPoint)Socket.RemoteEndPoint;
            Log(LogType.Debug,"Punched");
            Completed(true);
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

        protected override void Log(LogType type,string log)
        {
            string prefix = $"[TcpHolepunchState]: ";
            prefix += isInitiator ? "A: " : "B: ";
            base.Log(type,prefix + log);
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
