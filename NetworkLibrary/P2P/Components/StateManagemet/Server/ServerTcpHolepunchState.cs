using NetworkLibrary.P2P.Components.HolePunch;
using NetworkLibrary.P2P.Components.StateManagement;
using NetworkLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkLibrary.P2P.Components.StateManagemet.Server
{
    internal class ServerTcpHolepunchState : IState
    {
        public StateStatus Status { get; private set; }

        public Guid StateId { get; private set; }

        public event Action<IState> Completed;

        private StateManager stateManager;
        private Guid requesterId;
        private Guid destinationId;
        private IPEndPoint RequesterRemoteEp;
        private IPEndPoint DestinationRemoteEp;
        private EndpointTransferMessage requesterEndpoints;
        private EndpointTransferMessage destinationEndpoints;
        int requesterSuccess = 0;
        int destinationSuccess = 0;
        private byte[] random =  new byte[16];

        public ServerTcpHolepunchState(StateManager sm)
        {
            stateManager = sm;
           // var random = new byte[16];
            var rng = RandomNumberGenerator.Create();
            rng.GetNonZeroBytes(random);
            rng.Dispose();
        }
        public async void Initialize(MessageEnvelope message)
        {
            MiniLogger.Log(MiniLogger.LogLevel.Debug, "Initialising Server HP State");
            requesterEndpoints = KnownTypeSerializer.DeserializeEndpointTransferMessage(message.Payload, message.PayloadOffset);
            requesterId = message.From;
            destinationId = message.To;
            StateId = message.MessageId;

            MessageEnvelope msg = new MessageEnvelope
            {
                MessageId = StateId,
                From = requesterId,
                To = destinationId,
                Header = Constants.InitTCPHPRemote,
                IsInternal = true,
                Payload = random
            };

            stateManager.SendTcpMessage(destinationId, msg);
            MessageEnvelope msg2 = new MessageEnvelope
            {
                MessageId = StateId,
                Header = Constants.OkSendUdp,
                IsInternal = true,
                Payload = random
            };
            stateManager.SendTcpMessage(requesterId, msg2);
            await Task.Delay(3000);

            while (!portMapComplete && released == 0)
            {
                MiniLogger.Log(MiniLogger.LogLevel.Warning, "awaiting port map");
                if(DestinationRemoteEp == null)
                {
                    var msg1 = new MessageEnvelope()
                    {
                        MessageId = StateId,
                        To = destinationId,
                        Header = Constants.ResendUdp,
                        IsInternal = true,
                    };
                    stateManager.SendTcpMessage(destinationId,msg1);
                }
                if(RequesterRemoteEp == null)
                {
                    var msg1 = new MessageEnvelope()
                    {
                        MessageId = StateId,
                        To = requesterId,
                        Header = Constants.ResendUdp,
                        IsInternal = true,
                    };
                    stateManager.SendTcpMessage(requesterId, msg1);
                }
                await Task.Delay(3000);
            }
        }
        public void HandleMessage(MessageEnvelope message)
        {
            if(message.Header == Constants.Failed)
            {
                MiniLogger.Log(MiniLogger.LogLevel.Debug, "Fail Received");

                HandleFail(message);
            }
            else if(message.Header == Constants.Success)
            {
                MiniLogger.Log(MiniLogger.LogLevel.Debug, "Success Received");

                HandleSuccess(message);
            }
        }
        // here we need to do 4 try instead of 2
        // req fail dest fail, req fail dest fail = Fail
        // implement counter
        int requesterFailCnt = 0;
        int dstFailCnt = 0;
        private void HandleFail(MessageEnvelope message)
        {
            if (message.From == requesterId )
            {
                if( Interlocked.Increment(ref requesterFailCnt) == 1)
                {
                    MiniLogger.Log(MiniLogger.LogLevel.Debug, "Requester Failed, Destination will swap to client");
                    var msg = new MessageEnvelope()
                    {
                        MessageId = StateId,
                        To = destinationId,
                        Header = Constants.SwapToClient
                        ,
                        IsInternal = true,
                    };
                    stateManager.SendTcpMessage(destinationId, msg, s => KnownTypeSerializer.SerializeEndpointTransferMessage(s, requesterEndpoints));
                }
                else
                {
                    MiniLogger.Log(MiniLogger.LogLevel.Debug, "Requester Failed, Destination will swap to client");
                    var msg = new MessageEnvelope()
                    {
                        MessageId = StateId,
                        To = destinationId,
                        Header = Constants.SwapToClient
                        ,
                        IsInternal = true,
                    };
                    stateManager.SendTcpMessage(destinationId, msg, s => KnownTypeSerializer.SerializeEndpointTransferMessage(s, requesterEndpoints));

                }
                
            }
            else if (message.From == destinationId)
            {
                MiniLogger.Log(MiniLogger.LogLevel.Debug, "Destination Failed, Requester will swap to client");


                if (Interlocked.Increment(ref dstFailCnt) == 1)
                {
                    var msg = new MessageEnvelope()
                    {
                        MessageId = StateId,
                        To = requesterId,
                        Header = Constants.SwapToClient,
                        IsInternal = true,
                    };
                    stateManager.SendTcpMessage(requesterId, msg, s => KnownTypeSerializer.SerializeEndpointTransferMessage(s, destinationEndpoints));
                }
                else
                {
                    var msg = new MessageEnvelope()
                    {
                        MessageId = StateId,
                        To = destinationId,
                        Header = Constants.FinalizeFail,
                        IsInternal = true,
                    };
                    stateManager.SendTcpMessage(destinationId, msg);
                    msg.To = requesterId;
                    stateManager.SendTcpMessage(requesterId, msg);
                    Release(false);
                }


            }
        }

        private void HandleSuccess(MessageEnvelope message)
        {
           if (message.From == requesterId)
            {
                MiniLogger.Log(MiniLogger.LogLevel.Debug, "Requester Succeed");
                Interlocked.Exchange(ref requesterSuccess, 1);
            }
           else if(message.From == destinationId)
            {
                MiniLogger.Log(MiniLogger.LogLevel.Debug, "Destinastion Succeed");
                Interlocked.Exchange(ref destinationSuccess, 1);
            }
           if(Interlocked.CompareExchange(ref requesterSuccess,0,0) == 1 && Interlocked.CompareExchange(ref destinationSuccess, 0, 0) == 1)
            {
               
                var msg = new MessageEnvelope()
                {
                    MessageId = StateId,
                    Header = "FinalizeSuccess",
                    IsInternal = true,
                };
               // msg.Payload = random;
                stateManager.SendTcpMessage(destinationId, msg);
                stateManager.SendTcpMessage(requesterId, msg);
                Release(true);
            }
        }
        readonly object l =  new object();
        bool portMapComplete = false;   
        public void HandleMessage(IPEndPoint remoteEndpoint, MessageEnvelope message)
        {
            MiniLogger.Log(MiniLogger.LogLevel.Debug, "Udp Msg Received by HP module");
            lock (l)
            {
                if (message.Header == Constants.TcpPortMap)
                {
                    if(portMapComplete)
                    {
                        return;
                    }
                    if (message.From == requesterId && RequesterRemoteEp == null)
                    {
                        MiniLogger.Log(MiniLogger.LogLevel.Debug, "Requester Portmap");

                        RequesterRemoteEp = remoteEndpoint;
                        requesterEndpoints.IpRemote = remoteEndpoint.Address.GetAddressBytes();
                        requesterEndpoints.PortRemote = remoteEndpoint.Port;

                        var msg = new MessageEnvelope
                        {
                            MessageId = StateId,
                            Header = Constants.AckPortMap,
                            IsInternal = true,
                        };
                        stateManager.SendTcpMessage(requesterId, msg);

                    }
                    else if (message.From == destinationId && DestinationRemoteEp == null)
                    {
                        MiniLogger.Log(MiniLogger.LogLevel.Debug, "Destination Portmap");

                        DestinationRemoteEp = remoteEndpoint;
                        destinationEndpoints = KnownTypeSerializer.DeserializeEndpointTransferMessage(message.Payload, message.PayloadOffset);

                        destinationEndpoints.IpRemote = remoteEndpoint.Address.GetAddressBytes();
                        destinationEndpoints.PortRemote = remoteEndpoint.Port;

                        var msg = new MessageEnvelope
                        {
                            MessageId = StateId,
                            Header = Constants.AckPortMap,
                            IsInternal = true,
                        };
                        stateManager.SendTcpMessage(destinationId, msg);
                    }
                   
                    if (RequesterRemoteEp != null && DestinationRemoteEp != null)
                    {
                        portMapComplete = true;
                        var msg = new MessageEnvelope
                        {
                            From = requesterId,
                            MessageId = StateId,
                            Header = Constants.TryConnect,
                            IsInternal = true,
                        };
                        stateManager.SendTcpMessage(requesterId, msg, s => KnownTypeSerializer.SerializeEndpointTransferMessage(s, destinationEndpoints));
                    }


                }

            }
        }
        int released = 0;

        public void Release(bool isCompletedSuccessfully)
        {
            if (Interlocked.Increment(ref released) != 1)
            {
                return;
            }
            MiniLogger.Log(MiniLogger.LogLevel.Debug, "Releasing HP State");

            Status = StateStatus.Failed;

            if (isCompletedSuccessfully)
            {
                Status = StateStatus.Completed;
            }
            Completed?.Invoke(this);
            Completed = null;
        }

    }
}
