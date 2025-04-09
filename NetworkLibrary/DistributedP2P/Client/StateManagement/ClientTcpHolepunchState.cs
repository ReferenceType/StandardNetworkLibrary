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
    internal class ClientTcpHolepunchState : ConversationStateBase
    {
        private readonly Guid destId;
        private readonly IDistributedConnection connection;
        private readonly EndpointData serverEndpoint;
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
        private bool IsEstablished => Interlocked.CompareExchange(ref established, 0, 0) == 1;
        public ClientTcpHolepunchState(Guid stateId, Guid destId, IDistributedConnection connection, EndpointData serverEndpoint, ChannelInfo info) : base(stateId, 5000)
        {
            this.destId = destId;
            this.connection = connection;
            this.serverEndpoint = serverEndpoint;
            this.ChannelInfo = info;
        }

        //the initiator
        public void Start()
        {
            isInitiator = true;
            Log(StateId.ToString());

            localPort = StartTcpSocket();

            var msg = CreateEnvelope();
            msg.Header = InternalConstants.RequestSequentialHolepunchTcp;
            msg.KeyValuePairs = new Dictionary<string, string>();
            msg.KeyValuePairs["Port"] = localPort.ToString();
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
            }
        }

        // the destination peer of hp
        private void HandleRemoteHpRequest(MessageEnvelope message)
        {
            Log(StateId.ToString());

            ChannelInfo = new ChannelInfo();
            ChannelInfo.ChannelType = (ChannelType)int.Parse(message.KeyValuePairs["Type"]);
            ChannelInfo.ChannelName = message.KeyValuePairs["Name"];

            int port = StartTcpSocket();
            var msg = CreateEnvelope();
            msg.Header = InternalConstants.AckRequestHolepunchTcp;
            msg.KeyValuePairs = new Dictionary<string, string>();
            msg.KeyValuePairs["Port"] = port.ToString();

            if (ChannelInfo.RequiresKeyExchange())
                msg.KeyValuePairs["DH"] = Convert.ToBase64String(df.GetPublicKey());

            msg.To = destId;

            connection.SendAsyncMessage(msg);
        }

        private void StartHolepunchRoutine(MessageEnvelope message)
        {
            var epMsg = KnownTypeSerializer.DeserializeEndpointTransferMessage(message.Payload, message.PayloadOffset);
            var time = double.Parse(message.KeyValuePairs["Time"]);

            if (ChannelInfo.RequiresKeyExchange())
                otherPublicKey = Convert.FromBase64String(message.KeyValuePairs["DH"]);

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

            // use server ip, peer is on same network as server
            bool useServerIp = IPHelper.IsZero(epMsg.IpRemote);
            EndpointData publicEp = new EndpointData() { Ip = useServerIp ? serverEndpoint.Ip : epMsg.IpRemote, Port = epMsg.PortRemote };

            var now = connection.GetTime();
            var delay = time - now;
           
            Log("Delay: " + delay.ToString() + "ms");

            PreciseTimeAwaiter.Wait(delay);
            if (IsEstablished) return;

            var nextTryTime = connection.GetTime()+1500;

            for (int i = 0; i < 4; i++)
            {
                if (TryConnect(publicEp, (2000)))
                    return;
                //PreciseTimeAwaiter.Wait(nextTryTime - connection.GetTime());
                if (IsEstablished) return;
            }

        }


        private bool TryConnect(EndpointData endpoint, int timeoutMs = 600)
        {
            Socket connectSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                Log("Connecting to " + endpoint.ToIpEndpoint().ToString());
                connectSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                connectSocket.Bind(new IPEndPoint(IPAddress.Any, localPort));

                var connectTask = connectSocket.ConnectAsync(endpoint.ToIpEndpoint());
                var timeoutTask = Task.Delay(timeoutMs);

                if (Task.WhenAny(connectTask, timeoutTask).GetAwaiter().GetResult() == connectTask)
                {

                    connectSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);
                    HandleConnectedSocket(connectSocket);
                    Log($"Successfully connected to {endpoint.ToIpEndpoint()}");
                    return true;

                }
                else
                {
                    Log($"Connection to {endpoint.ToIpEndpoint()} timed out after {timeoutMs}ms");
                    connectSocket.Close();
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


        private int StartTcpSocket()
        {
            listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listeningSocket.SendBufferSize = 12800000;
            listeningSocket.ReceiveBufferSize = 12800000;
            listeningSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            listeningSocket.Bind(new IPEndPoint(IPAddress.Any, 0));

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
                Log("Failed Accept: " + e.Message);
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

            Log($"Successfully accepted {(IPEndPoint)socket.RemoteEndPoint}");
            acceptedSocket = socket;
         

            var msg = CreateEnvelope();
            msg.Header = InternalConstants.PunchSucces;
            msg.KeyValuePairs = new Dictionary<string, string>();
            msg.KeyValuePairs["Status"] = "Accepted";
            connection.SendAsyncMessage(msg);
        }

        private void TimedOut()
        {
            Log("Timed out");
            var msg = CreateEnvelope();
            msg.Header = InternalConstants.PunchFail;
            connection.SendAsyncMessage(msg);
            Cancel();
        }

        private void HandleFailure()
        {
            Log("Failed Punch");
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
            Log("Punched");
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

        private void Log(string log)
        {
            //return;
            string prefix = isInitiator ? "A: " : "B: ";
            Console.WriteLine(prefix + log);
        }


    }


}
