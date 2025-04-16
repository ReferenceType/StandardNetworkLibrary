using NetworkLibrary.Components.Crypto.DiffieHellman;
using NetworkLibrary.DistributedP2P.Components;
using NetworkLibrary.P2P.Components.HolePunch;
using NetworkLibrary.Utils;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetworkLibrary.DistributedP2P.Client.StateManagement
{
    class ClientPipeData
    {
        public ChannelInfo ChannelInfo { get; set; }
        public byte[] DHPublic;
    }
    internal class ClientPipeState : ConversationStateBase
    {
        private readonly IDistributedConnection connection;
        private readonly EndpointData serverEndpoint;
        private Guid destinationPeer;

        public Socket ConnectedSocket { get; private set; }
        public EndpointData SuccesfullEndpoint { get; private set; }
        public byte[] sharedSecret { get; private set; }

        public ChannelInfo ChannelInfo;
        private DiffieHellman df;
        private bool isInitiator;

        public ClientPipeState(Guid stateId, IDistributedConnection connection, EndpointData serverEndpoint, ChannelInfo info) : base(stateId, 20000)
        {
            this.connection = connection;
            this.serverEndpoint = serverEndpoint;
            this.ChannelInfo = info;
            isInitiator = true;
        }

        public ClientPipeState(MessageEnvelope message, IDistributedConnection connection, EndpointData serverEndpoint) : base(message.MessageId)
        {
            this.connection = connection;
            this.serverEndpoint = serverEndpoint;

        }

        public void Start(Guid destinationPeer)
        {
            bool tcp = ChannelInfo.ChannelType == ChannelType.Tcp || ChannelInfo.ChannelType == ChannelType.SecureTcp;
            this.destinationPeer = destinationPeer;

            var msg = CreateEnvelope();
            msg.Header = tcp ? InternalConstants.PipeRequestTcp : InternalConstants.PipeRequestUdp;
            msg.To = destinationPeer;

            var stream = SharerdMemoryStreamPool.RentStreamStatic();
            var data = GetPipeData();

            KnownTypeSerializer.SerializeClientPipeData(stream, data);
            msg.SetPayload(stream.GetBuffer(), 0, stream.Position32);

            connection.SendAsyncMessage(msg);
            SharerdMemoryStreamPool.ReturnStreamStatic(stream);

            Log("Requested Pipe");
        }

        public override void HandleMessage(MessageEnvelope message)
        {
            switch (message.Header)
            {
                // the listener
                case InternalConstants.PipeRequestTcp:
                case InternalConstants.PipeRequestUdp:
                    HandleConnectionRequest(message);
                    break;

                case InternalConstants.PipeTokenDeliveryTcp:
                    HandlePipeTokenTcp(message);
                    break;

                case InternalConstants.PipeTokenDeliveryUdp:
                    HandlePipeTokenUdp(message);
                    break;

                case InternalConstants.ConnectionAckGood:
                    HandleGoodAck(message);
                    break;

                case InternalConstants.ConnectionAckBad:
                    HandleBadAck(message);
                    break;
            }
        }

        private void HandleConnectionRequest(MessageEnvelope message)
        {
            Log("Connection Request Received");
            int offs = message.PayloadOffset;
            var pipeData = KnownTypeSerializer.DeserializeClientPipeData(message.Payload, ref offs);
            ChannelInfo = pipeData.ChannelInfo;

            var myData = GetPipeData();

            if (ChannelInfo.RequiresKeyExchange())
            {
                GenerateSharedSecret(pipeData.DHPublic);
            }

            var msg = CreateEnvelope();
            msg.Header = InternalConstants.PipeReqAck;

            var stream = SharerdMemoryStreamPool.RentStreamStatic();

            myData.ChannelInfo = null;

            KnownTypeSerializer.SerializeClientPipeData(stream, myData);
            msg.SetPayload(stream.GetBuffer(), 0, stream.Position32);

            connection.SendAsyncMessage(msg);
            SharerdMemoryStreamPool.ReturnStreamStatic(stream);

        }

        private async void HandlePipeTokenTcp(MessageEnvelope message)
        {
            Log("Handling Tcp Token");
            try
            {
                int off = message.PayloadOffset;
                var pipeData = KnownTypeSerializer.DeserializePipeData(message.Payload, ref off);

                if (pipeData.DHPublic != null)
                    GenerateSharedSecret(pipeData.DHPublic);

                EndpointData endpoint = pipeData.PipeEndpoint;

                if (IPHelper.IsZero(endpoint.Ip))
                    endpoint.Ip = serverEndpoint.Ip;

                Socket connected = await TryConnectWithTimeout(endpoint).ConfigureAwait(false);
                if (connected != null)
                {
                    bool success = await TokenExchange(connected, pipeData.Token);
                    if (success)
                    {
                        OnConnectionSuccessful(endpoint, connected);
                        return;
                    }
                    else
                    {
                        try { connected.Close(); connected.Dispose(); } catch { }
                    }
                }
                // connect send token
                //wait a data to come
                //then send ack

                Log($"Failed to exchange Tcp Token");
                OnConnectionFail();
                return;
            }
            catch (Exception e)
            {
                Log($"An Error occured while handling Tcp Token{e.Message}\n{e.StackTrace}");
                OnConnectionFail();
            }

        }

        private void GenerateSharedSecret(byte[] otherPublic)
        {
            sharedSecret = df.CalculateSharedSecret(otherPublic);
        }

        private async Task<Socket> TryConnectWithTimeout(EndpointData endpoint, int timeout = 5000)
        {
            var clientSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);

            var connectTask = ConnectAsync(clientSocket, endpoint.ToIpEndpoint());
            var timeoutTask = Task.Delay(timeout);

            var completedTask = await Task.WhenAny(connectTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                Log("Connection Failed");
                try { clientSocket.Close(); clientSocket.Dispose(); } catch { }
                return null;
            }

            bool res = connectTask.Result;
            if (res)
            {
                return clientSocket;
            }
            else
            {
                Log("Connection Failed");
                try { clientSocket.Close(); clientSocket.Dispose(); } catch { }
                return null;
            }

        }

        private Task<bool> ConnectAsync(Socket socket, IPEndPoint endPoint)
        {
            var tcs = new TaskCompletionSource<bool>();

            var sa = new SocketAsyncEventArgs();
            sa.RemoteEndPoint = endPoint;
            sa.Completed += (s, e) =>
            {
                if (e.SocketError == SocketError.Success)
                {
                    tcs.TrySetResult(true);
                }
                else
                {
                    tcs.TrySetResult(false);
                }
                sa.Dispose();
            };

            if (!socket.ConnectAsync(sa))
            {
                if (sa.SocketError == SocketError.Success)
                    tcs.TrySetResult(true);
            }

            return tcs.Task;
        }

        private async Task<bool> TokenExchange(Socket connectedSocket, byte[] token, int timeoutMs = 5000)
        {
            try
            {
                int bytesSent = await connectedSocket.SendAsync(new ArraySegment<byte>(token), SocketFlags.None);
                if (bytesSent != token.Length)
                {
                    Log("Tcp Token Send Failure");
                    return false;
                }

                var responseBuffer = new byte[1];
                var receiveTask = connectedSocket.ReceiveAsync(new ArraySegment<byte>(responseBuffer), SocketFlags.None);
                var timeoutTask = Task.Delay(timeoutMs);

                var completedTask = await Task.WhenAny(receiveTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    Log("Tcp Token Response Timeout");
                    return false;
                }

                int bytesReceived = receiveTask.Result;
                return bytesReceived == 1;

            }
            catch (Exception e)
            {
                Log($"An Error occured while exchanging Tcp Token{e.Message}\n{e.StackTrace}");
                return false;
            }
        }



        private async void HandlePipeTokenUdp(MessageEnvelope message)
        {
            try
            {
                int off = message.PayloadOffset;
                var pipeData = KnownTypeSerializer.DeserializePipeData(message.Payload, ref off);

                if (pipeData.DHPublic != null)
                    GenerateSharedSecret(pipeData.DHPublic);


                var connected = new Socket(SocketType.Dgram, ProtocolType.Udp);
                connected.SendBufferSize = 12800000;
                connected.ReceiveBufferSize = 12800000;

                EndpointData endpoint = pipeData.PipeEndpoint;


                if (IPHelper.IsZero(endpoint.Ip))
                    endpoint.Ip = serverEndpoint.Ip;

                bool success = await UdpTokenExchange(connected, pipeData.Token, endpoint.ToIpEndpoint());
                if (success)
                {
                    Log($"Connected");
                    OnConnectionSuccessful(endpoint, connected);
                    return;
                }
                else
                {
                    try { connected.Close(); connected.Dispose(); } catch { }
                }

                // connect send token
                //wait a data to come
                //then send ack


                Log($"Failed to exchange Udp Token");
                OnConnectionFail();
                return;
            }
            catch (Exception e)
            {
                Log($"An Error occured while handling Udp Token{e.Message}\n{e.StackTrace}");
                OnConnectionFail();
            }

        }

        private async Task<bool> UdpTokenExchange(Socket udpSocket, byte[] token, IPEndPoint remoteEndPoint, int timeoutMs = 3000)
        {
            try
            {
                udpSocket.Connect(remoteEndPoint);

                var sendTask = udpSocket.SendAsync(new ArraySegment<byte>(token), SocketFlags.None);
                var sendTimeout = Task.Delay(timeoutMs);

                if (await Task.WhenAny(sendTask, sendTimeout) == sendTimeout ||
                    sendTask.Result != token.Length)
                {
                    Log("Udp Token Send Timeout");
                    return false;
                }

                var responseBuffer = new byte[1024];
                var receiveTask = udpSocket.ReceiveAsync(new ArraySegment<byte>(responseBuffer), SocketFlags.None);
                var receiveTimeout = Task.Delay(timeoutMs);

                if (await Task.WhenAny(receiveTask, receiveTimeout) == receiveTimeout)
                {
                    Log("Udp Token Receive Timeout");
                    return false;
                }

                return receiveTask.Result == 1;
            }
            catch (Exception e)
            {
                Log($"An Error occured while exchanging Udp Token{e.Message}\n{e.StackTrace}");
                return false;
            }

        }


        private void OnConnectionSuccessful(EndpointData endpoint, Socket socket)
        {
            if (IsCompleted())
                return;

            this.ConnectedSocket = socket;
            this.SuccesfullEndpoint = endpoint;

            Log("Completed");

            var msg = CreateEnvelope();
            msg.Header = InternalConstants.ConnectionAckGood;
            connection.SendAsyncMessage(msg);
        }

        private void OnConnectionFail()
        {
            var msg = CreateEnvelope();
            msg.Header = InternalConstants.ConnectionAckBad;
            connection.SendAsyncMessage(msg);

            Log("Failed");

            Completed(false);
        }

        private void HandleGoodAck(MessageEnvelope message)
        {
            Completed(true);
        }

        private void HandleBadAck(MessageEnvelope message)
        {
            Completed(false);
        }

        private ClientPipeData GetPipeData()
        {
            ClientPipeData hpd = new ClientPipeData();
            hpd.ChannelInfo = ChannelInfo;

            if (ChannelInfo.RequiresKeyExchange())
            {
                df = new DiffieHellman();
                hpd.DHPublic = df.GetPublicKey();
            }
            return hpd;
        }
        protected override void Log(string log)
        {
            //return;
            string prefix = isInitiator ? "A: " : "B: ";
            base.Log(prefix + log);
        }

    }
}
