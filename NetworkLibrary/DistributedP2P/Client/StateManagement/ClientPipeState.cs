using NetworkLibrary.DistributedP2P.Components;
using NetworkLibrary.DistributedP2P.Server;
using NetworkLibrary.P2P.Components.HolePunch;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NetworkLibrary.Components.Crypto.DiffieHellman;

namespace NetworkLibrary.DistributedP2P.Client.StateManagement
{
    internal class ClientPipeState : ConversationStateBase
    {
        private readonly IDistributedConnection connection;
        private Guid destinationPeer;

        public Socket ConnectedSocket { get; private set; }
        public EndpointData SuccesfullEndpoint { get; private set; }
        public byte[] sharedSecret { get; private set; }

        public ChannelInfo ChannelInfo;
        private DiffieHellman df;
        public ClientPipeState(Guid stateId, IDistributedConnection connection,ChannelInfo info) : base(stateId, 20000)
        {
            this.connection = connection;
            this.ChannelInfo = info;
        }

        public ClientPipeState(MessageEnvelope message,IDistributedConnection connection) : base(message.MessageId)
        {
            this.connection = connection;
          
        }

        public void Start(Guid destinationPeer, bool tcp)
        {
            this.destinationPeer = destinationPeer;

            var msg = CreateEnvelope();
            msg.Header = tcp?InternalConstants.PipeRequestTcp:InternalConstants.PipeRequestUdp;
            msg.To = destinationPeer;
            msg.KeyValuePairs = new Dictionary<string, string>();
            msg.KeyValuePairs["Type"] = ((int)ChannelInfo.ChannelType).ToString();
            msg.KeyValuePairs["Name"] = ChannelInfo.ChannelName;

            if (ChannelInfo.RequiresKeyExchange())
            {
                df = new DiffieHellman();
                byte[] key = df.GetPublicKey();
                string keyStr = Convert.ToBase64String(key);
                msg.KeyValuePairs["DH"] = keyStr;
            }

            connection.SendAsyncMessage(msg);
        }

        public override void HandleMessage(MessageEnvelope message)
        {
            switch(message.Header)
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
            ChannelInfo = new ChannelInfo();
            ChannelInfo.ChannelType = (ChannelType)int.Parse(message.KeyValuePairs["Type"]);
            ChannelInfo.ChannelName = message.KeyValuePairs["Name"];

            var msg = CreateEnvelope();
            msg.Header = InternalConstants.PipeReqAck;
           
            if (ChannelInfo.RequiresKeyExchange())
            {
                df = new DiffieHellman();
                byte[] key = df.GetPublicKey();
                string keyStr = Convert.ToBase64String(key);

                msg.KeyValuePairs = new Dictionary<string, string>();
                msg.KeyValuePairs["DH"] = keyStr;
            }

            connection.SendAsyncMessage(msg);
        }

        private async void HandlePipeTokenTcp(MessageEnvelope message)
        {
            try
            {
                int off = message.PayloadOffset;
                var pipeData = KnownTypeSerializer.DeserializePipeData(message.Payload, ref off);

                if (ChannelInfo.RequiresKeyExchange())
                    GenerateSharedSecret(message.KeyValuePairs["DH"]);

                foreach (EndpointData endpoint in pipeData.PipeEndpoints)
                {
                    Socket connected = await TryConnectWithTimeout(endpoint);
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
                }

                OnConnectionFail();
                return;
            }
            catch
            {
                OnConnectionFail();
            }
            
        }

        private void GenerateSharedSecret(string otherPublic)
        {
             sharedSecret = df.CalculateSharedSecret(Convert.FromBase64String(otherPublic));
        }

        private async Task<Socket> TryConnectWithTimeout(EndpointData endpoint, int timeout = 500)
        {
            var clientSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            
            var connectTask = ConnectAsync(clientSocket, endpoint.ToIpEndpoint());
            var timeoutTask = Task.Delay(timeout);

            var completedTask = await Task.WhenAny(connectTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                try { clientSocket.Close(); clientSocket.Dispose(); } catch { }
                return null;
            }

            bool res =  connectTask.Result;
            if(res)
            {
                return clientSocket;
            }
            else
            {
                try { clientSocket.Close(); clientSocket.Dispose(); } catch { }
                return null;
            }

        }

        private async Task<bool> ConnectAsync(Socket socket, IPEndPoint endPoint)
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
                return sa.SocketError == SocketError.Success;
            }

            return await tcs.Task;
        }

        private async Task<bool> TokenExchange(Socket connectedSocket, byte[] token, int timeoutMs = 500)
        {
            try
            {
                connectedSocket.SendTimeout = timeoutMs;
                connectedSocket.ReceiveTimeout = timeoutMs;

                int bytesSent = await connectedSocket.SendAsync(new ArraySegment<byte>(token), SocketFlags.None);
                if (bytesSent != token.Length)
                {
                    return false;
                }

                var responseBuffer = new byte[1];
                var receiveTask = connectedSocket.ReceiveAsync(new ArraySegment<byte>(responseBuffer), SocketFlags.None);
                var timeoutTask = Task.Delay(timeoutMs);

                var completedTask = await Task.WhenAny(receiveTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    return false;
                }

                int bytesReceived = receiveTask.Result;

                connectedSocket.SendTimeout = -1;
                connectedSocket.ReceiveTimeout = -1;

                return bytesReceived == 1;
            }
            catch
            {
                return false;
            }
        }



        private async void HandlePipeTokenUdp(MessageEnvelope message)
        {

            if (ChannelInfo.RequiresKeyExchange())
                GenerateSharedSecret(message.KeyValuePairs["DH"]);

            int off = message.PayloadOffset;
            var pipeData = KnownTypeSerializer.DeserializePipeData(message.Payload, ref off);

            var connected = new Socket(SocketType.Dgram, ProtocolType.Udp);
            connected.SendBufferSize = 12800000;
            connected.ReceiveBufferSize = 12800000;

            foreach (EndpointData endpoint in pipeData.PipeEndpoints)
            {
                
                bool success = await UdpTokenExchange(connected, pipeData.Token, endpoint.ToIpEndpoint());
                if (success)
                {
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
            }

            OnConnectionFail();
            return;
        }

        private async Task<bool> UdpTokenExchange(Socket udpSocket, byte[] token, IPEndPoint remoteEndPoint, int timeoutMs = 500)
        {
            try
            {
                udpSocket.Connect(remoteEndPoint);

                var sendTask = udpSocket.SendAsync(new ArraySegment<byte>(token), SocketFlags.None);
                var sendTimeout = Task.Delay(timeoutMs);

                if (await Task.WhenAny(sendTask, sendTimeout) == sendTimeout ||
                    sendTask.Result != token.Length)
                {
                    return false;
                }

                var responseBuffer = new byte[1024];
                var receiveTask = udpSocket.ReceiveAsync(new ArraySegment<byte>(responseBuffer), SocketFlags.None);
                var receiveTimeout = Task.Delay(timeoutMs);

                if (await Task.WhenAny(receiveTask, receiveTimeout) == receiveTimeout)
                {
                    return false;
                }

                return receiveTask.Result == 1;
            }
            catch
            {
                return false;
            }
           
        }


        private void OnConnectionSuccessful(EndpointData endpoint, Socket socket)
        {
            if (IsCompleted())
                return;

            this.ConnectedSocket = socket;
            this.SuccesfullEndpoint = endpoint;

            var msg = CreateEnvelope();
            msg.Header = InternalConstants.ConnectionAckGood;
            connection.SendAsyncMessage(msg);
        }

        private void OnConnectionFail()
        {
            var msg = CreateEnvelope();
            msg.Header = InternalConstants.ConnectionAckBad;
            connection.SendAsyncMessage(msg);

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

    }
}
