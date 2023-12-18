using NetworkLibrary.Components;
using NetworkLibrary.MessageProtocol;
using NetworkLibrary.MessageProtocol.Serialization;
using NetworkLibrary.P2P.Components.HolePunch;
using NetworkLibrary.TCP.AES;
using NetworkLibrary.TCP.Base;
using NetworkLibrary.UDP;
using NetworkLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkLibrary.P2P.Components.StateManagement.Client
{
    internal class ClientTcpHolepunchState : IState
    {
        enum Sts
        {
            Initiated,
            Listening,
            Connecting,
            Awaiting,
            SwappedToClient,
            SendingUdp,
            Finalising,
            Idle,
            ResendingUdp,
            Finalised,
            Releasing,
            FinalisingClient,
            AwaitingKey
        }
        private Sts internalState;
        public Guid destinationId;
        public Guid selfId;
        StateManager stateManager;
        private IPEndPoint relayUdpEp;
        EndPoint selfLocalEndpoint;
        private Socket selfSocket;
        public bool connected;
        public bool accepted;
        public AesTcpServer selfServer;
        public AesTcpClient selfClient;

        private EndpointTransferMessage remoteEndpoints;

        public byte[] AesKey;
        public StateStatus Status { get; private set; }

        public Guid StateId { get; private set; }

        public event Action<IState> Completed;
        private bool isInitiator;
        int PortmapAcked =0;
        public ClientTcpHolepunchState(StateManager stateManager, IPEndPoint relayUdpEp,Guid stateId)
        {
           this.StateId = stateId;
            this.stateManager = stateManager;
            this.relayUdpEp = relayUdpEp;
            //DebugSts();
        }

        private async void DebugSts()
        {
            await Task.Delay(5000);

            while (released == 0)
            {
                await Task.Delay(1000);
                Console.WriteLine(internalState.ToString());
            }
        }

        /*
         * Create socket bind then send eps.
         * server will create state for you
         * then will give you ok signal which this one will send a udp message to determine port mapping
         * then server will send you set of destination endpoints
         * try connect and fail
         * if failed start listening 
         * then notify server about fail
         * if listening socket gets something within the timeout notify succes
         * finalize(event etc)
         */
        public void InitiateByLocal(Guid requesterId,Guid destinationId)
        {
            isInitiator = true;
            this.destinationId = destinationId;
            selfId = requesterId;

            InitialiseSocket(new IPEndPoint(IPAddress.Any, 0));
            selfLocalEndpoint = selfSocket.LocalEndPoint as IPEndPoint;//new IPEndPoint(IPAddress.Any, ((IPEndPoint)selfSocket.LocalEndPoint).Port);

            MessageEnvelope msg = new MessageEnvelope()
            {
                Header = Constants.ReqTCPHP,
                MessageId = StateId,
                IsInternal = true,
            };

            EndpointTransferMessage epmsg = new EndpointTransferMessage()
            {
                LocalEndpoints = GetLocalEndpoints(selfLocalEndpoint)
            };

            stateManager.SendTcpMessage(destinationId, msg, stream =>
            {
                KnownTypeSerializer.SerializeEndpointTransferMessage(stream,epmsg);
            });
            MiniLogger.Log(MiniLogger.LogLevel.Debug, selfLocalEndpoint + " Initiated");
            internalState = Sts.Initiated;
        }

        // init tcp remote
        public void InitiateByRemote(MessageEnvelope message)
        {
            internalState = Sts.Initiated;

            AesKey = ByteCopy.ToArray(message.Payload, message.PayloadOffset, message.PayloadCount);
            
            destinationId = message.From;
            selfId = message.To;

            selfServer = new AesTcpServer(new IPEndPoint(IPAddress.Any, 0), new ConcurrentAesAlgorithm(AesKey));
            selfServer.OnClientAccepted += Accepted;
            selfServer.GatherConfig = ScatterGatherConfig.UseBuffer;
            selfServer.StartServer();
            selfLocalEndpoint = new IPEndPoint(IPAddress.Any,/* 11123*/selfServer.LocalEndpoint.Port);
            // selfLocalEndpoint = new IPEndPoint(IPAddress.Any,11123);
            internalState = Sts.SendingUdp;
            SendUdpPortMapMsg();
            MiniLogger.Log(MiniLogger.LogLevel.Debug, selfLocalEndpoint + " Handling Request");

        }


        public void HandleMessage(MessageEnvelope message)
        {
            MiniLogger.Log(MiniLogger.LogLevel.Debug, message.Header);
            try
            {
                switch (message.Header)
                {
                    case Constants.ResendUdp:
                        if (AesKey == null)
                        {
                            internalState = Sts.AwaitingKey;
                            return;
                        }
                        internalState = Sts.ResendingUdp;
                        SendUdpPortMapMsg();
                        break;
                    case Constants.OkSendUdp:
                        AesKey = ByteCopy.ToArray(message.Payload, message.PayloadOffset, message.PayloadCount);
                        internalState = Sts.SendingUdp;
                        SendUdpPortMapMsg();
                        break;
                    case Constants.AckPortMap:
                        Interlocked.Exchange(ref PortmapAcked, 1);
                        break;
                    case Constants.TryConnect:
                        internalState = Sts.Connecting;
                        TryConnectDestEndpoints(message);
                        break;
                    case Constants.SwapToClient:
                        internalState = Sts.SwappedToClient;
                        SwapToClient(message);
                        break;
                    case Constants.FinalizeSuccess:
                        ThreadPool.UnsafeQueueUserWorkItem((s) => {
                            internalState = Sts.Finalising;
                            Finalize(message);
                        },null);
                       
                        break;
                    case Constants.FinalizeFail:
                        Release(false);
                        break;
                }

            }
            catch (Exception e)
            {

            }
        }

        private void InitialiseSocket(EndPoint toBind)
        {
            lock (globalLock)
            {
                var s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                s.NoDelay = true;
                s.ReceiveBufferSize = 12800000;
                s.Bind(toBind);
                Interlocked.Exchange(ref selfSocket, s);
            }
           
        }
        private void SwapToClient(MessageEnvelope message)
        {
            MiniLogger.Log(MiniLogger.LogLevel.Debug, selfLocalEndpoint + " Swapping to client");

            selfServer.ShutdownServer();
            Thread.Sleep(1);
            InitialiseSocket(selfLocalEndpoint);

            TryConnectDestEndpoints(message);
        }
       

        private void TryConnectDestEndpoints(MessageEnvelope message)
        {
            remoteEndpoints = KnownTypeSerializer.DeserializeEndpointTransferMessage(message.Payload, message.PayloadOffset);
            remoteEndpoints = FilterEndpoints(remoteEndpoints);
            ThreadPool.QueueUserWorkItem((s) =>
            {
                foreach (var lep in remoteEndpoints.LocalEndpoints)
                {
                    if (AttemptToConnect(lep))
                        break;
                }
                if (!connected)
                {
                    if(!AttemptToConnect(new EndpointData { Ip = remoteEndpoints.IpRemote, Port = remoteEndpoints.PortRemote }, 3000))
                    {
                        var msg = new MessageEnvelope()
                        {
                            Header = Constants.Failed,
                            MessageId = StateId,
                            IsInternal = true,
                        };
                        SwapToServer();
                        stateManager.SendTcpMessage(destinationId, msg);
                    }
                }

            },null);
          
        }

        private bool AttemptToConnect(EndpointData lep, int timeout = 500)
        {
            if (connected)
            {
                return true;
            }
            MiniLogger.Log(MiniLogger.LogLevel.Debug, selfLocalEndpoint + " Attempting to connect" + lep.ToIpEndpoint().ToString());

            try
            {
                var ep = lep.ToIpEndpoint();
                //selfSocket.Connect(ep);
                //Connected(null);

                var sa = new SocketAsyncEventArgs();
                sa.Completed += Connected;
                sa.RemoteEndPoint = ep;
                if (!selfSocket.ConnectAsync(sa))
                {
                    Connected(null, sa);
                }

                if (!connectHandle.WaitOne(timeout))
                {
                    selfSocket.Close();
                    InitialiseSocket(selfLocalEndpoint);
                    return false;
                }
                return true;

            }
            catch
            {
                return selfSocket.Connected;
                  
            }
           
        }
        ManualResetEvent connectHandle = new ManualResetEvent(false);
        private void Connected(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                return;
            }
                
            connectHandle.Set();
            connected = true;
            MiniLogger.Log(MiniLogger.LogLevel.Debug, "Connected!");

            var msg = new MessageEnvelope()
            {
                Header = Constants.Success,
                MessageId = StateId,
                IsInternal = true,
            };
            stateManager.SendTcpMessage(destinationId, msg);
            MiniLogger.Log(MiniLogger.LogLevel.Debug, "Sending Success");
        }

        private void SwapToServer()
        {
            internalState = Sts.Listening;

            MiniLogger.Log(MiniLogger.LogLevel.Debug, "Swapping To Server");
            selfSocket.Dispose();
            selfServer = new AesTcpServer((IPEndPoint)selfLocalEndpoint, new ConcurrentAesAlgorithm(AesKey));
            selfServer.GatherConfig = ScatterGatherConfig.UseBuffer;
            selfServer.OnClientAccepted += Accepted;
            selfServer.StartServer();
        }

        private void Accepted(Guid guid)
        {
            MiniLogger.Log(MiniLogger.LogLevel.Debug, "Accepted");
            accepted = true;
            var msg = new MessageEnvelope()
            {
                Header = Constants.Success,
                MessageId = StateId,
                IsInternal= true,
            };
            stateManager.SendTcpMessage(destinationId, msg);
            MiniLogger.Log(MiniLogger.LogLevel.Debug, "Sending Success");

        }

        GenericMessageSerializer<MockSerializer> serializer = new GenericMessageSerializer<MockSerializer>();
        PooledMemoryStream stream = new PooledMemoryStream();
        AsyncUdpClient cl = new AsyncUdpClient();
        private static object globalLock = new object();
        private void SendUdpPortMapMsg()
        {
            try
            {
                lock (globalLock)
                {
                    if (!cl.clientSocket.IsBound)
                    {
                        cl.clientSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, false);
                        cl.clientSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                        cl.Bind(((IPEndPoint)selfLocalEndpoint));
                    }
                }
               
                
                MessageEnvelope message = new MessageEnvelope()
                {
                    Header = Constants.TcpPortMap,
                    MessageId = StateId,
                    From = selfId
                };
                stream.Position = 0;
                stream.WriteByte(92);
                stream.WriteByte(93);
                EndpointTransferMessage epmsg = new EndpointTransferMessage()
                {
                    LocalEndpoints = GetLocalEndpoints(selfLocalEndpoint)
                };
                serializer.EnvelopeMessageWithInnerMessage(stream, message, s =>
                {
                    KnownTypeSerializer.SerializeEndpointTransferMessage(s, epmsg);
                });
                var buffer = stream.GetBuffer();
                int cnt = stream.Position32;
                cl.SendTo(buffer, 0, cnt, relayUdpEp);
                stream.Position32 = 0;
            }
            catch (Exception ex)
            {
                MiniLogger.Log(MiniLogger.LogLevel.Error, "Error occured when sending udp port map : " + ex.Message);
                //Release(false);
            }
           
        }
        private void Finalize(MessageEnvelope message)
        {
            MiniLogger.Log(MiniLogger.LogLevel.Debug, "Finalising");
            // if connected make a client
            // if accepted make a server.
            //AesKey = ByteCopy.ToArray(message.Payload, message.PayloadOffset, message.PayloadCount);
            if (connected)
            {
                internalState = Sts.FinalisingClient;
                
                selfClient = new AesTcpClient(selfSocket, new ConcurrentAesAlgorithm(AesKey));
            }
            Release(true);
        }
       

        public void HandleMessage(IPEndPoint remoteEndpoint, MessageEnvelope message)
        {
           // udp ignored.
        }
        int released = 0;
        public void Release(bool isCompletedSuccessfully)
        {
            internalState = Sts.Releasing;

            if (Interlocked.Increment(ref released) != 1)
            {
                return;
            }
            if (!isCompletedSuccessfully)
            {
                Status = StateStatus.Failed;
                try
                {
                    selfServer?.ShutdownServer();
                    selfSocket?.Dispose();
                    cl?.Dispose();
                }
                catch { }
            }
            else 
            {
                Status = StateStatus.Completed;
            }

            Completed?.Invoke(this);
            Completed = null;
        }

        internal List<EndpointData> GetLocalEndpoints(EndPoint ep)
        {

            List<EndpointData> endpoints = new List<EndpointData>();
            var lep = (IPEndPoint)ep;

            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    if (ip.ToString() == "0.0.0.0")
                        continue;
                    endpoints.Add(new EndpointData()
                    {
                        Ip = ip.GetAddressBytes(),
                        Port = lep.Port
                    });
                }
            }
            return endpoints;
        }

        internal EndpointTransferMessage FilterEndpoints(EndpointTransferMessage endpoints)
        {
            List<EndpointData> toRemove = new List<EndpointData>();
            foreach (var item in endpoints.LocalEndpoints)
            {
                if (IPUtils.IsLoopback(item.Ip))
                {
                    toRemove.Add(item);
                }
            }
            foreach (var rem in toRemove)
            {
                endpoints.LocalEndpoints.Remove(rem);
            }
            if (IPUtils.IsPrivate(endpoints.IpRemote))
            {
                endpoints.IpRemote = relayUdpEp.Address.GetAddressBytes();
            }
            return endpoints;
        }

    }
}
