using NetworkLibrary.DistributedP2P.Components;
using NetworkLibrary.P2P.Components.HolePunch;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetworkLibrary.DistributedP2P.Client.StateManagement
{
    internal class ClientPipeState : ConversationStateBase
    {
        private readonly IClientConnection connection;
        private Guid destinationPeer;

        public Socket ConnectedSocket { get; private set; }
        public EndpointData SuccesfullEndpoint { get; private set; }

        public ClientPipeState(Guid stateId, IClientConnection connection) : base(stateId)
        {
            this.connection = connection;
        }

        public void Start(Guid destinationPeer)
        {
            this.destinationPeer = destinationPeer;
            var msg = CreateEnvelope();
            msg.Header = InternalConstants.PipeRequest;
            msg.To = destinationPeer;

            connection.SenAsyncMessage(msg);
        }

        public override void HandleMessage(MessageEnvelope message)
        {
            switch(message.Header)
            {
                case InternalConstants.PipeTokenDelivery:
                    HandlePipeToken(message);
                    break;

                case InternalConstants.ConnectionAckGood:
                    HandleGoodAck(message);
                    break;

                case InternalConstants.ConnectionAckBad:
                    HandleBadAck(message);
                    break;
            }
        }

        private async void HandlePipeToken(MessageEnvelope message)
        {
            int off = message.PayloadOffset;
            var pipeData = KnownTypeSerializer.DeserializePipeData(message.Payload, ref off);

            foreach (EndpointData endpoint in pipeData.PipeEndpoints) 
            {
                Socket connected = await TryConnectWithTimeout(endpoint);
                if(connected != null)
                {
                    bool success = await TokenExchange(connected,pipeData.Token);
                    if(success)
                    {
                        OnConnectionSuccessful(endpoint,connected);
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

        private void OnConnectionSuccessful(EndpointData endpoint, Socket socket)
        {
            if (IsCompleted())
                return;

            this.ConnectedSocket = socket;
            this.SuccesfullEndpoint = endpoint;

            var msg = CreateEnvelope();
            msg.Header = InternalConstants.ConnectionAckGood;
            connection.SenAsyncMessage(msg);
        }

        private void OnConnectionFail()
        {
            var msg = CreateEnvelope();
            msg.Header = InternalConstants.ConnectionAckBad;
            connection.SenAsyncMessage(msg);

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
