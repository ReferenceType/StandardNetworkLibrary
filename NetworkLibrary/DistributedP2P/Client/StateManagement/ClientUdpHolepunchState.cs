using NetworkLibrary.Components;
using NetworkLibrary.Components.Crypto.DiffieHellman;
using NetworkLibrary.DistributedP2P.Channels.Components;
using NetworkLibrary.DistributedP2P.Components;
using NetworkLibrary.P2P.Components.HolePunch;
using NetworkLibrary.Utils;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkLibrary.DistributedP2P.Client.StateManagement
{
    //Todo 0.0.0.0 means server ip!
    internal class ClientUdpHolepunchState : ConversationStateBase
    {
        private readonly Guid destId;
        private readonly IDistributedConnection connection;
        private readonly EndpointData serverEndpoint;
        private readonly EndpointData discoveryServerendPoint;
        public Socket Socket;
        private bool isInitiator;

        public IPEndPoint SuccesfulEndpoint;

        private DiffieHellman df = new DiffieHellman();
        private byte[] otherPublicKey;
        public byte[] SharedSecret;
        public ChannelInfo ChannelInfo;
        private IPEndPoint selfRemoteEp;
        private IPEndPoint selfLocalEp;

        private int conditionCount = 0;

        public ClientUdpHolepunchState(Guid stateId, Guid destId, IDistributedConnection connection, EndpointData serverEndpoint, EndpointData discoveryServerendPoint, ChannelInfo info, ILogger logger) : base(stateId, 20000, logger)
        {
            this.destId = destId;
            this.connection = connection;
            this.serverEndpoint = serverEndpoint;
            this.discoveryServerendPoint = discoveryServerendPoint;
            this.ChannelInfo = info;
        }

        //the initiator
        public async void Start()
        {
            isInitiator = true;
            Log(LogType.Debug, StateId.ToString());

            await StartUdpSocket();

            var msg = CreateEnvelope();
            msg.Header = InternalConstants.RequestHolepunchUdp;
            msg.To = destId;

            var stream = SharerdMemoryStreamPool.RentStreamStatic();
            KnownTypeSerializer.SerializeHolepunchData(stream, GetHpData());

            msg.SetPayload(stream.GetBuffer(), 0, stream.Position32);
            connection.SendAsyncMessage(msg);

            SharerdMemoryStreamPool.ReturnStreamStatic(stream);
        }



        public override void HandleMessage(MessageEnvelope message)
        {
            try
            {
                switch (message.Header)
                {
                    case InternalConstants.RequestHolepunchUdp:
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
            catch (Exception ex)
            {
                OnError(ex.Message + "\n" + ex.StackTrace);
            }

        }

        // the destination peer of hp
        private async void HandleRemoteHpRequest(MessageEnvelope message)
        {
            Log(LogType.Debug, StateId.ToString());

            int offs = message.PayloadOffset;
            var hpData = KnownTypeSerializer.DeserializeHolepunchData(message.Payload, ref offs);

            ChannelInfo = hpData.ChannelInfo;

            await StartUdpSocket();

            var msg = CreateEnvelope();
            msg.Header = InternalConstants.AckRequestHolepunchUdp;
            msg.To = destId;

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

            int offs = message.PayloadOffset;
            var hpData = KnownTypeSerializer.DeserializeHolepunchData(message.Payload, ref offs);

            var epMsg = hpData.Endpoints;
            otherPublicKey = hpData.DHPublic;

            var time = double.Parse(message.KeyValuePairs["Time"]);
            SignalCompletionCondition();

            if (epMsg.LocalEndpoints.Count > 0)
            {
                //var now0 = connection.GetTime();
                //var delay0 = (time - now0) / 4;
                Thread.Sleep(50);

                foreach (EndpointData localEp in epMsg.LocalEndpoints)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        TryPunch(localEp, MessageFlags.HP);
                        Thread.Sleep(100);
                        if (IsCompleted()) return;
                    }
                }
            }


            if (IsCompleted()) return;

            // use server ip, peer is on same network as server
            bool useServerIp = IPHelper.IsZero(epMsg.IpRemote);
            EndpointData publicEp = new EndpointData() { Ip = useServerIp ? serverEndpoint.Ip : epMsg.IpRemote, Port = epMsg.PortRemote };


            var now = connection.GetTime();
            var delay = time - now;
            if (delay > 500)
                delay = 0;

            Log(LogType.Debug, "Delay: " + delay.ToString() + "ms");
            PreciseTimeAwaiter.Wait(delay);
            if (IsCompleted()) return;

            for (int i = 0; i < 8; i++)
            {
                TryPunch(publicEp, MessageFlags.HP);
                PreciseTimeAwaiter.Wait(20 + (20 * i * i));
                if (IsCompleted()) return;
            }

        }

        private void TryPunch(EndpointData ep, MessageFlags flag)
        {
            var ipep = ep.ToIpEndpoint();
            TryPunch(ipep, flag);
        }
        private object m = new object();
        PooledMemoryStream stream = new PooledMemoryStream();

        private void TryPunch(IPEndPoint ep, MessageFlags flag)
        {
            lock (m)
            {

                Log(LogType.Debug, $"Sending {flag.ToString()}To " + ep.ToString());

                stream.Position = 0;
                stream.WriteByte((byte)flag);
                var epd = new EndpointData(ep);
                KnownTypeSerializer.SerializeEndpointData(stream, epd);

                Socket.SendTo(stream.GetBuffer(), stream.Position32, SocketFlags.None, ep);
            }

        }

        private async Task StartUdpSocket()
        {

            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            Socket.SendBufferSize = 12800000;
            Socket.ReceiveBufferSize = 12800000;
            Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, true);

            Socket.Bind(new IPEndPoint(IPAddress.Any, 0));

            selfLocalEp = (IPEndPoint)Socket.LocalEndPoint;

            var remoteEp = await EndpointDiscoveryClient.GetUdpPublicEndpoint(Socket, discoveryServerendPoint.ToIpEndpoint(), 3000);
            if (remoteEp == null)
            {
                Log(LogType.Warning, "Failed to get public endpoint");
                remoteEp = new EndpointData("0.0.0.0", selfLocalEp.Port);
            }
            selfRemoteEp = remoteEp.ToIpEndpoint();

            Receive();


        }
        private async void Receive()
        {
            var buffer = BufferPool.RentBuffer(64000);
            int receivedOnce = 0;
            int receivedAck = 0;
            while (true)
            {
                try
                {
                    var remoteEP = (EndPoint)new IPEndPoint(IPAddress.Any, 0);
                    var receiveTask = Socket.ReceiveFromAsync(new ArraySegment<byte>(buffer), SocketFlags.None, remoteEP);

                    var completedTask = await Task.WhenAny(receiveTask, Task.Delay(5000));
                    if (completedTask == receiveTask)
                    {
                        SocketReceiveFromResult received = await receiveTask;

                        if (buffer[0] == (byte)MessageFlags.HP)
                        {
                            // this must be only once
                            if (Interlocked.CompareExchange(ref receivedOnce, 1, 0) == 0)
                            {
                                var ipep = (IPEndPoint)received.RemoteEndPoint;
                                Log(LogType.Debug, "[-]Received 0xFF from " + ipep.ToString());
                                TryPunch((IPEndPoint)received.RemoteEndPoint, MessageFlags.HPAck);
                            }

                        }
                        else if (buffer[0] == (byte)MessageFlags.HPAck)
                        {
                            Log(LogType.Debug, "[+]Received 0x0F From " + ((IPEndPoint)received.RemoteEndPoint).ToString());

                            if (Interlocked.CompareExchange(ref receivedAck, 1, 0) == 0)
                            {
                                TryPunch((IPEndPoint)received.RemoteEndPoint, MessageFlags.HPAck);
                                ReceivedBidirectional(received.RemoteEndPoint);
                            }
                            return;
                        }
                        else
                        {
                            Log(LogType.Debug, "Cancel1");
                            Cancel();
                            return;
                        }
                    }
                    else
                    {
                        TimedOut();
                        return;
                    }
                }
                catch (Exception e)
                {
                    Log(LogType.Debug, "ERROR" + e.Message);
                    Cancel();
                    return;
                }
                finally
                {
                    BufferPool.ReturnBuffer(buffer);
                }
            }

        }
        // only called once when succesfuly received.
        private void ReceivedBidirectional(EndPoint remoteEndPoint)
        {

            if (remoteEndPoint == null) return;

            var ipep = (IPEndPoint)remoteEndPoint;

            if (Interlocked.CompareExchange(ref SuccesfulEndpoint, ipep, null) != null)
                return;


            var msg = CreateEnvelope();
            msg.Header = InternalConstants.PunchSucces;
            connection.SendAsyncMessage(msg);

            SignalCompletionCondition();
        }

        private void TimedOut()
        {
            Log(LogType.Debug, "Timed out");
            var msg = CreateEnvelope();
            msg.Header = InternalConstants.PunchFail;
            connection.SendAsyncMessage(msg);
            Cancel();
        }

        private void OnError(string error)
        {
            Log(LogType.Debug, "Exception :" + error);
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
        // Need consesus!
        private void HandleRemoteSucces(MessageEnvelope message)
        {
            SignalCompletionCondition();
        }

        private void SignalCompletionCondition()
        {
            if (Interlocked.Increment(ref conditionCount) == 3)
            {
                if (ChannelInfo.RequiresKeyExchange())
                    SharedSecret = df.CalculateSharedSecret(otherPublicKey);

                if (SuccesfulEndpoint == null)
                    throw new Exception("Endpont is null");

                Log(LogType.Debug, "Punched");
                Completed(true);
            }

        }

        protected override void Completed(bool succes)
        {
            base.Completed(succes);
            if (!IsSuccesful)
            {
                try
                {
                    Socket?.Close();
                    Socket?.Dispose();
                }
                catch { }
            }
        }

        protected override void Log(LogType type, string log)
        {
            string prefix = $"[UdpHolepunchState]: ";
            prefix += isInitiator ? "A: " : "B: ";
            base.Log(type, prefix + log);
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
