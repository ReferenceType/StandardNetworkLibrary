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

        public IPEndPoint SuccesfulEndpoint { get; private set; }

        public ClientUdpHolepunchState(Guid stateId, Guid destId, IDistributedConnection connection) : base(stateId, 20000)
        {
            this.destId = destId;
            this.connection = connection;
        }

        //the initiator
        public void Start()
        {
            int port = StartUdpSocket();

            var msg = CreateEnvelope();
            msg.Header = InternalConstants.RequestHolepunchUdp;
            msg.KeyValuePairs = new Dictionary<string, string>();
            msg.KeyValuePairs[port.ToString()] = null;
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
                    HandleSucces();
                    break;
                case InternalConstants.PunchFailAck:
                    HandleFailure();
                    break;
            }
        }





        // the destination of hp
        private void HandleRemoteHpRequest(MessageEnvelope message)
        {
            int port = StartUdpSocket();
            var msg = CreateEnvelope();
            msg.Header = InternalConstants.AckRequestHolepunchUdp;
            msg.KeyValuePairs = new Dictionary<string, string>();
            msg.KeyValuePairs[port.ToString()] = null;
            msg.To = destId;

            connection.SendAsyncMessage(msg);
        }


        private void StartHolepunchRoutine(MessageEnvelope message)
        {
            var epMsg = KnownTypeSerializer.DeserializeEndpointTransferMessage(message.Payload, message.PayloadOffset);
            var time = double.Parse(message.KeyValuePairs["Time"]);
          
            foreach (EndpointData localEp in epMsg.LocalEndpoints)
            {
                for (int i = 0; i < 2; i++)
                {
                    TryPunch(localEp);
                    PreciseTimeAwaiter.Wait(20);
                }
            }

            EndpointData publicEp = new EndpointData() { Ip = epMsg.IpRemote, Port = epMsg.PortRemote };

            var now = connection.GetTime();
            PreciseTimeAwaiter.Wait(time - now);

            for (int i = 0; i < 4; i++)
            {
                TryPunch(publicEp);
                PreciseTimeAwaiter.Wait(20 * i);
            }

        }

        private void TryPunch(EndpointData localEp)
        {
            Socket.SendTo(new byte[1] { 0xFF }, SocketFlags.None, localEp.ToIpEndpoint());
        }

        private int StartUdpSocket()
        {
            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            Socket.Bind(new IPEndPoint(IPAddress.Any, 0));

            ReceiveOnceAsync().ContinueWith(Received);

            return ((IPEndPoint)Socket.LocalEndPoint).Port;
  
        }

        private async Task<IPEndPoint> ReceiveOnceAsync()
        {
            var buffer = BufferPool.RentBuffer(64000);
            var remoteEP = (EndPoint)new IPEndPoint(IPAddress.Any, 0);

            try
            {
                var receiveTask = Socket.ReceiveFromAsync(new ArraySegment<byte>(buffer), SocketFlags.None, remoteEP);

                var completedTask = await Task.WhenAny(receiveTask, Task.Delay(5000));
                if (completedTask == receiveTask)
                {
                    var result = await receiveTask; 
                    return ((IPEndPoint)result.RemoteEndPoint);
                }
                else
                {
                    TimedOut();
                    return null;
                }
            }
            catch
            {
                Cancel();
                return null;
            }
            finally
            {
                BufferPool.ReturnBuffer(buffer);
            }
        }

        private void Received(Task<IPEndPoint> task)
        {
            SuccesfulEndpoint = task.Result;
            var msg = CreateEnvelope();
            msg.Header = InternalConstants.PunchSucces;
            connection.SendAsyncMessage(msg);
        }

        private void TimedOut()
        {
            var msg = CreateEnvelope();
            msg.Header = InternalConstants.PunchFail;
            connection.SendAsyncMessage(msg);
        }

        private void HandleFailure()
        {
            Completed(false);
        }

        private void HandleSucces()
        {
            Completed(true);
        }

      


    }

      
}
