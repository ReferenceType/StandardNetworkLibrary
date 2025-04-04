using NetworkLibrary.Components.Crypto.DiffieHellman;
using NetworkLibrary.DistributedP2P.Components;
using NetworkLibrary.DistributedP2P.Server;
using NetworkLibrary.P2P.Components.HolePunch;
using NetworkLibrary.UDP;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkLibrary.DistributedP2P.Client.StateManagement
{
    internal class ClientUdpHolepunchState : ConversationStateBase
    {
        private readonly Guid destId;
        private readonly IDistributedConnection connection;
        public Socket Socket;
        private bool remoteSucces;
        private bool isInitiator;

        public IPEndPoint SuccesfulEndpoint;

        private DiffieHellman df = new DiffieHellman();
        private byte[] otherPublicKey;
        public byte[] SharedSecret;
        public ChannelInfo info;
        public ClientUdpHolepunchState(Guid stateId, Guid destId, IDistributedConnection connection, ChannelInfo info) : base(stateId, 20000)
        {
            this.destId = destId;
            this.connection = connection;
            this.info = info;
        }

        //the initiator
        public void Start()
        {
            isInitiator = true;
            int port = StartUdpSocket();

            var msg = CreateEnvelope();
            msg.Header = InternalConstants.RequestHolepunchUdp;
            msg.KeyValuePairs = new Dictionary<string, string>();
            msg.KeyValuePairs["Port"] = port.ToString();
            msg.KeyValuePairs["Type"] = ((int)info.ChannelType).ToString();
            msg.KeyValuePairs["Name"] = info.ChannelName;

            if(info.RequiresKeyExchange())
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

                case InternalConstants.StartHPUdp:
                    message.LockBytes();
                    ThreadPool.UnsafeQueueUserWorkItem((s) => StartHolepunchRoutine(message), null);
                    break;

                case InternalConstants.PunchSuccesAck:
                    HandleRemoteSucces();
                    break;
                case InternalConstants.PunchFailAck:
                    HandleFailure();
                    break;
            }
        }

        // the destination peer of hp
        private void HandleRemoteHpRequest(MessageEnvelope message)
        {
            info = new ChannelInfo();
            info.ChannelType = (ChannelType)int.Parse(message.KeyValuePairs["Type"]);
            info.ChannelName = message.KeyValuePairs["Name"];

            int port = StartUdpSocket();
            var msg = CreateEnvelope();
            msg.Header = InternalConstants.AckRequestHolepunchUdp;
            msg.KeyValuePairs = new Dictionary<string, string>();
            msg.KeyValuePairs["Port"] = port.ToString();

            if (info.RequiresKeyExchange())
                msg.KeyValuePairs["DH"] = Convert.ToBase64String(df.GetPublicKey());

            msg.To = destId;

            connection.SendAsyncMessage(msg);
        }


        private void StartHolepunchRoutine(MessageEnvelope message)
        {
            var epMsg = KnownTypeSerializer.DeserializeEndpointTransferMessage(message.Payload, message.PayloadOffset);
            var time = double.Parse(message.KeyValuePairs["Time"]);
            if(info.RequiresKeyExchange())
                otherPublicKey = Convert.FromBase64String(message.KeyValuePairs["DH"]);

           
            foreach (EndpointData localEp in epMsg.LocalEndpoints)
            {
                for (int i = 0; i < 2; i++)
                {
                    TryPunch(localEp, UdpFlags.HP);
                    PreciseTimeAwaiter.Wait(20);
                    if (IsCompleted()) return;
                }
            }

            if (IsCompleted()) return;
            EndpointData publicEp = new EndpointData() { Ip = epMsg.IpRemote, Port = epMsg.PortRemote };

            var now = connection.GetTime();
            var delay = now - time;
            if(delay>500)
                delay = 0;
            PreciseTimeAwaiter.Wait(delay);
            if (IsCompleted()) return;

            for (int i = 0; i < 5; i++)
            {
                TryPunch(publicEp,UdpFlags.HP);
                PreciseTimeAwaiter.Wait(20 * i*i);
                if (IsCompleted()) return;
            }

        }

        private void TryPunch(EndpointData ep, UdpFlags flag )
        {
            var ipep = ep.ToIpEndpoint();
            TryPunch(ipep, flag);
        }

        private void TryPunch(IPEndPoint ep, UdpFlags flag)
        {
            Log("SendingTo " + ep.ToString());
            Socket.SendTo(new byte[1] { (byte)flag }, SocketFlags.None, ep);
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
                        if (received.ReceivedBytes != 1)
                        {
                            Cancel();
                            return;
                        }

                        if (buffer[0] == (byte)UdpFlags.HP)
                        {
                            // this must be only once
                            if (Interlocked.CompareExchange(ref receivedOnce, 1,0) ==0)
                                TryPunch((IPEndPoint)received.RemoteEndPoint, UdpFlags.HPAck);

                            Log("[-]Received 0xFF from " + ((IPEndPoint)received.RemoteEndPoint).ToString());
                        }
                        else if (buffer[0] == (byte)UdpFlags.HPAck)
                        {
                            ReceivedBidirectional(received.RemoteEndPoint);
                            return;
                        }
                        else
                        {
                            Cancel();
                        }
                    }
                    else
                    {
                        TimedOut();
                    }
                }
                catch
                {
                    Cancel();
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

            Log("Received From " + SuccesfulEndpoint.ToString());

            var msg = CreateEnvelope();
            msg.Header = InternalConstants.PunchSucces;
            connection.SendAsyncMessage(msg);
        }

        private void TimedOut()
        {
            var msg = CreateEnvelope();
            msg.Header = InternalConstants.PunchFail;
            connection.SendAsyncMessage(msg);
            Cancel();
        }

        private void HandleFailure()
        {
            Completed(false);
        }

        private void HandleRemoteSucces()
        {
            // Completed(true);

            remoteSucces = true;
            CheckSucces();
        }

        private void CheckSucces() 
        {
            if (SuccesfulEndpoint != null && remoteSucces)
            {
                if (info.RequiresKeyExchange())
                    SharedSecret = df.CalculateSharedSecret(otherPublicKey);

                Completed(true);
            }
        }
      
        private void Log(string log)
        {
            string prefix = isInitiator ? "A: " : "B: ";
            Console.WriteLine(prefix+log);
        }


    }

      
}
