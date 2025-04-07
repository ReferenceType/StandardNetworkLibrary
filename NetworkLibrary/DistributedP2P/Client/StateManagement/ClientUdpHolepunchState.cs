using NetworkLibrary.Components;
using NetworkLibrary.Components.Crypto.DiffieHellman;
using NetworkLibrary.DistributedP2P.Components;
using NetworkLibrary.DistributedP2P.Server;
using NetworkLibrary.P2P.Components.HolePunch;
using NetworkLibrary.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public Socket Socket;
        private bool isInitiator;

        public IPEndPoint SuccesfulEndpoint;

        private DiffieHellman df = new DiffieHellman();
        private byte[] otherPublicKey;
        public byte[] SharedSecret;
        public ChannelInfo ChannelInfo;
        public ClientUdpHolepunchState(Guid stateId, Guid destId, IDistributedConnection connection, EndpointData serverEndpoint, ChannelInfo info) : base(stateId, 20000)
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

            int port = StartUdpSocket();

            var msg = CreateEnvelope();
            msg.Header = InternalConstants.RequestHolepunchUdp;
            msg.KeyValuePairs = new Dictionary<string, string>();
            msg.KeyValuePairs["Port"] = port.ToString();
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

        // the destination peer of hp
        private void HandleRemoteHpRequest(MessageEnvelope message)
        {
            Log(StateId.ToString());

            ChannelInfo = new ChannelInfo();
            ChannelInfo.ChannelType = (ChannelType)int.Parse(message.KeyValuePairs["Type"]);
            ChannelInfo.ChannelName = message.KeyValuePairs["Name"];

            int port = StartUdpSocket();
            var msg = CreateEnvelope();
            msg.Header = InternalConstants.AckRequestHolepunchUdp;
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
                var now0 = connection.GetTime();
                var delay0 = (time - now0) / 4;
                PreciseTimeAwaiter.Wait(delay0);

                foreach (EndpointData localEp in epMsg.LocalEndpoints)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        TryPunch(localEp, UdpFlags.HP);
                        PreciseTimeAwaiter.Wait(20);
                        if (IsCompleted()) return;
                    }
                }
            }
           

            if (IsCompleted()) return;

            // use server ip, peer is on same network as server
            bool useServerIp = IPHelper.IsZero(epMsg.IpRemote);
            EndpointData publicEp = new EndpointData() { Ip = useServerIp?serverEndpoint.Ip: epMsg.IpRemote, Port = epMsg.PortRemote };
            

            var now = connection.GetTime();
            var delay = time - now;
            if (delay > 500)
                delay = 0;

            Log("Delay: " + delay.ToString() + "ms");
            PreciseTimeAwaiter.Wait(delay);
            if (IsCompleted()) return;

            for (int i = 0; i < 5; i++)
            {
                TryPunch(publicEp, UdpFlags.HP);
                PreciseTimeAwaiter.Wait(20 * i * i);
                if (IsCompleted()) return;
            }

        }

        private void TryPunch(EndpointData ep, UdpFlags flag)
        {
            var ipep = ep.ToIpEndpoint();
            TryPunch(ipep, flag);
        }
        private object m = new object();
        PooledMemoryStream stream = new PooledMemoryStream();
        private void TryPunch(IPEndPoint ep, UdpFlags flag)
        {
            lock (m)
            {

                Log($"Sending {flag.ToString() }To " + ep.ToString());
                
                stream.Position = 0;
                stream.WriteByte((byte)flag);
                var epd = new EndpointData(ep);
                KnownTypeSerializer.SerializeEndpointData(stream, epd);

                Socket.SendTo(stream.GetBuffer(),stream.Position32, SocketFlags.None, ep);
            }

        }

        private int StartUdpSocket()
        {
            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            Socket.SendBufferSize = 12800000;
            Socket.ReceiveBufferSize = 12800000;
            Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, true);

            Socket.Bind(new IPEndPoint(IPAddress.Any, 0));

            Receive();

            return ((IPEndPoint)Socket.LocalEndPoint).Port;
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
                        SocketReceiveFromResult received = receiveTask.Result;
                        
                        if (buffer[0] == (byte)UdpFlags.HP)
                        {
                            // this must be only once
                            if (Interlocked.CompareExchange(ref receivedOnce, 1, 0) == 0)
                            {
                                var ipep = (IPEndPoint)received.RemoteEndPoint;
                                Log("[-]Received 0xFF from " + ipep.ToString());
                                TryPunch((IPEndPoint)received.RemoteEndPoint, UdpFlags.HPAck);                               
                            }

                        }
                        else if (buffer[0] == (byte)UdpFlags.HPAck)
                        {
                            Log("[+]Received 0x0F From " + ((IPEndPoint)received.RemoteEndPoint).ToString());

                            if (Interlocked.CompareExchange(ref receivedAck, 1, 0) == 0)
                            {
                                TryPunch((IPEndPoint)received.RemoteEndPoint, UdpFlags.HPAck);
                                ReceivedBidirectional(received.RemoteEndPoint);
                            }
                            return;
                        }
                        else
                        {
                            Log("Cancel1");
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
                catch(Exception e)
                {
                    Log("ERROR" + e.Message);
                    Cancel();
                    return;
                }
                finally
                {
                    BufferPool.ReturnBuffer(buffer);
                }
            }

        }

        private void ReceivedBidirectional(EndPoint remoteEndPoint)
        {

            if (remoteEndPoint == null) return;

            var ipep = (IPEndPoint)remoteEndPoint;

            if (Interlocked.CompareExchange(ref SuccesfulEndpoint, ipep, null) != null)
                return;

            var msg = CreateEnvelope();
            msg.Header = InternalConstants.PunchSucces;
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
           
            if (ChannelInfo.RequiresKeyExchange())
                SharedSecret = df.CalculateSharedSecret(otherPublicKey);

            Log("Punched");
            Completed(true);
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

        private void Log(string log)
        {
            return;
            string prefix = isInitiator ? "A: " : "B: ";
            Console.WriteLine(prefix + log);
        }


    }


}
