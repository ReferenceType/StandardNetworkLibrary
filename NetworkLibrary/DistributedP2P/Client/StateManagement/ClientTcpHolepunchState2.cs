using NetworkLibrary.Components.Crypto.DiffieHellman;
using NetworkLibrary.DistributedP2P.Components;
using NetworkLibrary.DistributedP2P.Server;
using NetworkLibrary.P2P.Components.HolePunch;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkLibrary.DistributedP2P.Client.StateManagement
{
    //Todo 0.0.0.0 means server ip!
    internal class ClientTcpHolepunchState2 : ConversationStateBase
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
        public byte[] SharedSecret;
        public ChannelInfo ChannelInfo;

        private int localPort;
        private Socket listeningSocket;
        private Socket acceptedSocket;
        private Socket connectedSocket;
        private int established = 0;
        private int connected = 0;
        private int accepted = 0;

        IPEndPoint selfEndpoint = new IPEndPoint(IPAddress.Any,0);
        int swapCnt = 0;

        bool isListening = false;
        List<EndpointData> localEndpoints =  new List<EndpointData>();
        EndpointData publicEndpoint;
        private EndpointData selfRemoteEp;

        private bool IsEstablished => Interlocked.CompareExchange(ref established, 0, 0) == 1;
        public ClientTcpHolepunchState2(Guid stateId, Guid destId, IDistributedConnection connection, EndpointData serverEndpoint,EndpointData discoveryServerEp, ChannelInfo info) : base(stateId, 10000)
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

            selfRemoteEp = await BindPort();
            if (selfRemoteEp == null)
                return;

            localPort = selfEndpoint.Port;

            Log("Bound on port " + localPort);
            Log("Remote port " + selfRemoteEp.Port);

            var msg = CreateEnvelope();
            msg.Header = InternalConstants.RequestSimultaneousHolepunchTcp;
            msg.KeyValuePairs = new Dictionary<string, string>();
           // msg.KeyValuePairs["PortLocal"] = selfEndpoint.ToString();
            msg.KeyValuePairs["Port"] = selfRemoteEp.Port.ToString();
            msg.KeyValuePairs["Type"] = ((int)ChannelInfo.ChannelType).ToString();
            msg.KeyValuePairs["Name"] = ChannelInfo.ChannelName;

            if (ChannelInfo.RequiresKeyExchange())
                msg.KeyValuePairs["DH"] = Convert.ToBase64String(df.GetPublicKey());

            msg.To = destId;

            connection.SendAsyncMessage(msg);
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
                case InternalConstants.PunchSwap:
                    Swap();
                    break;
            }
        }

        // the destination peer of hp
        private async void HandleRemoteHpRequest(MessageEnvelope message)
        {
            Log(StateId.ToString());

            ChannelInfo = new ChannelInfo();
            ChannelInfo.ChannelType = (ChannelType)int.Parse(message.KeyValuePairs["Type"]);
            ChannelInfo.ChannelName = message.KeyValuePairs["Name"];

            selfRemoteEp = await BindPort();
            if (selfRemoteEp == null)
                return;


            localPort = StartTcpListener();
            Log("listening on port " + localPort);


            var msg = CreateEnvelope();
            msg.Header = InternalConstants.AckRequestHolepunchTcp;
            msg.KeyValuePairs = new Dictionary<string, string>();
            msg.KeyValuePairs["Port"] = selfRemoteEp.Port.ToString();

            if (ChannelInfo.RequiresKeyExchange())
                msg.KeyValuePairs["DH"] = Convert.ToBase64String(df.GetPublicKey());

            msg.To = destId;

            connection.SendAsyncMessage(msg);
        }

        private void StartHolepunchRoutine(MessageEnvelope message)
        {
            if (IsCompleted()) return;

            var epMsg = KnownTypeSerializer.DeserializeEndpointTransferMessage(message.Payload, message.PayloadOffset);
            localEndpoints = epMsg.LocalEndpoints;

            bool useServerIp = IPHelper.IsZero(epMsg.IpRemote);
            publicEndpoint = new EndpointData() { Ip = useServerIp ? serverEndpoint.Ip : epMsg.IpRemote, Port = epMsg.PortRemote };

            if (ChannelInfo.RequiresKeyExchange())
                otherPublicKey = Convert.FromBase64String(message.KeyValuePairs["DH"]);

            ////213.243.208.162
            //foreach (var ep in localEndpoints)
            //{
            //    ep.Ip[0] = 213;
            //    ep.Ip[1] = 243;
            //    ep.Ip[2] = 208;
            //    ep.Ip[3] = 162;

            //    publicEndpoint.Ip[0] = 213;
            //    publicEndpoint.Ip[1] = 243;
            //    publicEndpoint.Ip[2] = 208;
            //    publicEndpoint.Ip[3] = 162;
            //}

            if (!isListening)
            {
                TryPunch();
            }

        }

        private void TryPunch()
        {
            try
            {
                // if there are local endpoints to test
                if (localEndpoints.Count > 0)
                {
                    //foreach (EndpointData localEp in localEndpoints)
                    //{
                    //    if (TryConnect(localEp, 500))
                    //        return;

                    //    if (IsCompleted()) return;

                    //}
                }

                for (int i = 0; i < 1; i++)
                {
                    if (TryConnect(publicEndpoint, (2000)))
                        return;

                    if (IsCompleted()) return;

                }
            }
            catch { }
            finally
            {
                if(Interlocked.CompareExchange(ref established,0,0) == 0)
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
                catch {
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
                ThreadPool.UnsafeQueueUserWorkItem( _ => TryPunch(), null);
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
                connectSocket.Bind(selfEndpoint);

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

        private async Task<EndpointData> BindPort()
        {
            var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            clientSocket.Bind(selfEndpoint);

            var remoteEp = await EndpointDiscoveryClient.GetTcpPublicEndpoint(clientSocket,discoveryServerEp.ToIpEndpoint(),5000);
            if (remoteEp == null)
            {
                Log("Failed to get public endpoint");
                return null;
            }
            selfEndpoint = (IPEndPoint)clientSocket.LocalEndPoint;


            try
            {
                clientSocket?.Close();
                clientSocket?.Dispose();
                clientSocket = null;
            }
            catch { }

            return remoteEp;

        }

        private int StartTcpListener()
        {
            listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listeningSocket.SendBufferSize = 12800000;
            listeningSocket.ReceiveBufferSize = 12800000;

            listeningSocket.Bind(selfEndpoint);

            selfEndpoint = (IPEndPoint)listeningSocket.LocalEndPoint;


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
            Thread.Sleep(100);
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
            if(Socket == null)
            {
                Log("Failed to get socket");
                Completed(false);
                return;
            }
            SuccesfulEndpoint = (IPEndPoint)Socket.RemoteEndPoint;
            Log("Punched");
            Completed(true);
        }

        public override void Cancel()
        {
            lock(cancellationMutex)
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

        private void Log(string log)
        {
            //return;
            string prefix = isInitiator ? "A: " : "B: ";
            Console.WriteLine(prefix + log);
        }


    }


}
