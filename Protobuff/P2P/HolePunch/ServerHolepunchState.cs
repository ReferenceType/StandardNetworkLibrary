using NetworkLibrary;
using NetworkLibrary.MessageProtocol;
using NetworkLibrary.MessageProtocol.Serialization;
using NetworkLibrary.Utils;
using ProtoBuf;
using Protobuff.P2P.StateManagemet;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Protobuff.P2P.HolePunch
{
    [ProtoContract]
    internal class ChanneCreationMessage : IProtoMessage
    {
        [ProtoMember(1)]
        public byte[] SharedSecret { get; set; }
        [ProtoMember(2)]
        public Guid RegistrationId { get; set; }
        [ProtoMember(3)]
        public Guid DestinationId { get; set; }

        [ProtoMember(4)]
    //    [DefaultValue(true)]
        public bool Encrypted { get; set; }

    }

    [ProtoContract]
    internal class EndpointTransferMessage : IProtoMessage
    {
        [ProtoMember(1)]
        public byte[] IpRemote { get; set; }
        [ProtoMember(2)]
        public int PortRemote { get; set; }
        [ProtoMember(3)]
        public List<EndpointData> LocalEndpoints { get; set; } = new List<EndpointData>();
    }


    [ProtoContract]
    public class EndpointData
    {
        [ProtoMember(1)]
        public byte[] Ip;
        [ProtoMember(2)]
        public int Port;

        public IPEndPoint ToIpEndpoint()
        {
            return new IPEndPoint(new IPAddress(Ip), Port);
        }

        public EndpointData()
        {

        }

        public EndpointData(IPEndPoint ep)
        {
            Ip = ep.Address.GetAddressBytes();
            Port = ep.Port;
        }
    }

    internal class ServerHolepunchState:IState
    {
        public event Action<IState> Completed;
        static RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();

        private SecureProtoRelayServer Server;
        public Guid StateId { get; }

        public StateStatus Status { get; private set; }

        // public TaskCompletionSource<IState> Completion { get; } =  new TaskCompletionSource<IState>();

        private readonly Guid requesterId;
        private readonly Guid destinationId;
        private int requesterPunchSucess;
        private int destinationPunchSucess;
        private IPEndPoint RequesterEndpoint;
        private EndpointTransferMessage RequesterLocalEndpoints;
        private IPEndPoint DestinationEndpoint;
        private EndpointTransferMessage DestinationLocalEndpoints;

        private int IsWaitingUdpMessage;
        private object locker = new object();

        public ServerHolepunchState(SecureProtoRelayServer server, Guid stateId, Guid requesterId, Guid destinationId, bool encrypted = true)
        {
            Server = server;
            this.StateId = stateId;
            this.requesterId = requesterId;
            this.destinationId = destinationId;
           
#if DEBUG

            MiniLogger.Log(MiniLogger.LogLevel.Info, "------ Server State Encyrption enabled : " + encrypted.ToString());
#endif
            CreateChannels(encrypted);
        }

        //private async void StartLifetimeCounter(int lifeSpanMs)
        //{
        //    await Task.Delay(lifeSpanMs).ConfigureAwait(false);
        //    Completed?.Invoke(this);
           
        //    OnComplete = null;
        //}

        public void HandleMessage(MessageEnvelope msg)
        {
#if DEBUG

            MiniLogger.Log(MiniLogger.LogLevel.Info, "server hp msg handled: "+ msg.Header);
#endif

            if (msg.Header == Constants.SuccesAck)
                HanldePunchAck(msg);
        }
      
        public void HandleMessage(IPEndPoint ep, MessageEnvelope e)
        {
#if DEBUG

            MiniLogger.Log(MiniLogger.LogLevel.Info, "server hp udpmsg handled");
#endif
            HandleEndpointRegisteryMesssage(ep, e);
        }

        // 1. An initiation message comes here
        // both initiator and destination receives a udp socket creation msg.
        public void CreateChannels(bool encrypted)
        {
#if DEBUG

            MiniLogger.Log(MiniLogger.LogLevel.Info, "server hp cmd create chhannel encyrption : " + encrypted.ToString());
#endif

            var channelCreationMsgRequester = new ChanneCreationMessage()
            {
                SharedSecret = Server.ServerUdpInitKey,
                RegistrationId = StateId,
                DestinationId = destinationId,
                Encrypted = encrypted
            };
            var channelCreationMsgDestination = new ChanneCreationMessage()
            {
                SharedSecret = Server.ServerUdpInitKey,
                RegistrationId = StateId,
                DestinationId = requesterId,
                Encrypted = encrypted
            };

            var envelope = GetEnvelope(Constants.CreateChannel);
            var envelope2 = GetEnvelope(Constants.CreateChannel);

            Interlocked.Exchange(ref IsWaitingUdpMessage, 1);
            BeginTimeoutCounter(500);

           

            Server.SendAsyncMessage(requesterId, envelope,
                (stream)=> KnownTypeSerializer.SerializeChannelCreationMessage(stream, channelCreationMsgRequester));
            Server.SendAsyncMessage(destinationId, envelope2, 
                (stream) => KnownTypeSerializer.SerializeChannelCreationMessage(stream, channelCreationMsgDestination));

        }


        // 2.After initiation they both should send a udp msg, so that the server can retrieve their remote endpoints. 
        // if package drops we retry with timeout few times.
        public void HandleEndpointRegisteryMesssage(EndPoint ep, MessageEnvelope envelope)
        {
#if DEBUG
            MiniLogger.Log(MiniLogger.LogLevel.Info, "server hp ep registered");
#endif
            if (Interlocked.CompareExchange(ref IsWaitingUdpMessage,0,0)==0)
                return;

            if (envelope.From == requesterId)
            {
                var re = ep as IPEndPoint;
                if(Interlocked.CompareExchange(ref RequesterEndpoint,re, null) == null)
                {
                    var rl = KnownTypeSerializer.DeserializeEndpointTransferMessage(envelope.Payload, envelope.PayloadOffset);
                    Interlocked.CompareExchange(ref RequesterLocalEndpoints, rl, null);
                }
               
            }
            else if (envelope.From == destinationId)
            {
                var de = ep as IPEndPoint;
                if(Interlocked.CompareExchange(ref DestinationEndpoint, de, null) == null)
                {
                    var dl = KnownTypeSerializer.DeserializeEndpointTransferMessage(envelope.Payload, envelope.PayloadOffset);
                    Interlocked.CompareExchange(ref DestinationLocalEndpoints, dl, null);
                }
            }
            var requesterEp = Interlocked.CompareExchange(ref RequesterEndpoint, null, null);
            var destinationEp = Interlocked.CompareExchange(ref DestinationEndpoint, null, null);
            if (requesterEp != null && destinationEp != null)
            {
#if DEBUG
                MiniLogger.Log(MiniLogger.LogLevel.Info, "requester port:" + requesterEp.Port);
                MiniLogger.Log(MiniLogger.LogLevel.Info, "target port:" + destinationEp.Port);
#endif
                Interlocked.Exchange(ref IsWaitingUdpMessage, 0);

                SendStartPunchingMessage();
            }

        }

        // this will ask for udp resend 3 times in case of package loss.
        private async void BeginTimeoutCounter(int timeout)
        {
            for (int i = 0; i < 4; i++)
            {
                await Task.Delay(timeout).ConfigureAwait(false);
               
                    if (Interlocked.CompareExchange(ref IsWaitingUdpMessage,1,1)==1)
                        AskForUdpResend();
                
            }
        }

        private void AskForUdpResend()
        {
#if DEBUG

            MiniLogger.Log(MiniLogger.LogLevel.Info, "$$ server hp udp resend");
#endif
            MessageEnvelope envelope = GetEnvelope(Constants.HoplePunchUdpResend);

            if (RequesterEndpoint == null)
            {
                envelope.MessageId = StateId;
                Server.SendAsyncMessage(requesterId, envelope);
            }
            if (DestinationEndpoint == null)
            {
                envelope.MessageId = StateId;
                Server.SendAsyncMessage(destinationId, envelope);
            }

        }

        // 3. Once we collected both endpoint both sides should receive
        // their target endpoints. Peers should start punching at that point.
        public void SendStartPunchingMessage()
        {
#if DEBUG

            MiniLogger.Log(MiniLogger.LogLevel.Info, "server hp start message ");
#endif

            MessageEnvelope envelope = GetEnvelope(Constants.HoplePunch);
            MessageEnvelope envelope1 = GetEnvelope(Constants.HoplePunch);

            var epTransferMessageRequester = new EndpointTransferMessage()
            {
                IpRemote = DestinationEndpoint.Address.GetAddressBytes(),
                PortRemote = DestinationEndpoint.Port,
                LocalEndpoints = DestinationLocalEndpoints.LocalEndpoints
            };
            var epTransferMessageDestination = new EndpointTransferMessage()
            {
                IpRemote = RequesterEndpoint.Address.GetAddressBytes(),
                PortRemote = RequesterEndpoint.Port,
                LocalEndpoints = RequesterLocalEndpoints.LocalEndpoints
            };

            Server.SendAsyncMessage(destinationId, envelope,
                (stream)=> KnownTypeSerializer.SerializeEndpointTransferMessage(stream, epTransferMessageDestination));
            Server.SendAsyncMessage(requesterId,envelope1,
                (stream) => KnownTypeSerializer.SerializeEndpointTransferMessage(stream, epTransferMessageRequester));

        }

        // if both sides acks positive(both received msg from their udp holes) punch is sucessfull.
        // we also send their private Aes key here.
        // note that enire handsake is under an ssl client so its safe.
        EndpointData msgRequesterSucces = new EndpointData();
        EndpointData msgDestinationSucces = new EndpointData();
        int completed = 0;


        private void HanldePunchAck(MessageEnvelope m)
        {
            if (Interlocked.CompareExchange(ref completed, 0, 0) == 1)
                return;

            if (m.From == requesterId)
            {
                if (Interlocked.CompareExchange(ref requesterPunchSucess, 1, 0) == 1) return;
                var rs = KnownTypeSerializer.DeserializeEndpointData(m.Payload,m.PayloadOffset);
                Interlocked.Exchange(ref msgRequesterSucces, rs);
            }
            else if (m.From == destinationId)
            {
                if (Interlocked.CompareExchange(ref destinationPunchSucess, 1, 0) == 1) return;

                var ds = KnownTypeSerializer.DeserializeEndpointData(m.Payload, m.PayloadOffset);
                Interlocked.Exchange(ref msgDestinationSucces, ds);

            }

            if (Interlocked.CompareExchange(ref requesterPunchSucess,1,1)==1 
                && Interlocked.CompareExchange(ref destinationPunchSucess, 1, 1) == 1)
            {
                if (Interlocked.CompareExchange(ref completed, 1, 0) == 1)
                    return;
                Release(true);
                //Completion.TrySetResult(this);
                var mdst = GetEnvelope(Constants.SuccessFinalize);
                mdst.KeyValuePairs = new Dictionary<string, string>();
                mdst.KeyValuePairs["IP"] = new IPAddress( msgRequesterSucces.Ip).ToString();
                mdst.KeyValuePairs["Port"] = msgRequesterSucces.Port.ToString();

                var mreq = GetEnvelope(Constants.SuccessFinalize);
                mreq.KeyValuePairs = new Dictionary<string, string>();
                mreq.KeyValuePairs["IP"] = new IPAddress( msgDestinationSucces.Ip).ToString();
                mreq.KeyValuePairs["Port"] = msgDestinationSucces.Port.ToString();

                var sharedSecret = new byte[16];
                rng.GetNonZeroBytes(sharedSecret);
                mdst.Payload = sharedSecret;
                mreq.Payload = sharedSecret;

                Server.SendAsyncMessage(destinationId, mdst);
                Server.SendAsyncMessage(requesterId, mreq);
               
                MiniLogger.Log( MiniLogger.LogLevel.Info,$"Succecfully punched hole between {mdst.KeyValuePairs["IP"]} and {mreq.KeyValuePairs["IP"]}");
            }
        }

        private MessageEnvelope GetEnvelope(string header)
        {
            return new MessageEnvelope()
            { Header = header, IsInternal = true, MessageId = StateId };

        }
        private int isReleased = 0;
        public void Release(bool isCompletedSuccessfully)
        {
            if (Interlocked.CompareExchange(ref isReleased, 1, 0) == 0)
            {
                if (isCompletedSuccessfully)
                    Status = StateStatus.Completed;
                else
                    Status = StateStatus.Failed;

                Completed?.Invoke(this);
                Completed = null;
            }
        }
    }
}
