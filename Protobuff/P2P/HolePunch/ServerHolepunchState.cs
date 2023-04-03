using NetworkLibrary.Utils;
using ProtoBuf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Protobuff.P2P.HolePunch
{
    internal class HolepunchHeaders
    {
        public const string CreateChannel = "%CC";
        public const string HoplePunch = "%HP";
        public const string HoplePunchUdpResend = "%RHP";

        public const string HolePunchRequest = "%HPR";
        public const string SuccesAck = "%SA";
        public const string SuccessFinalize = "%SF";
    }

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
        [DefaultValue(true)]
        public bool Encrypted { get; set; } = true;

    }

    [ProtoContract]
    internal class EndpointTransferMessage : IProtoMessage
    {
        [ProtoMember(1)]
        public string IpRemote;
        [ProtoMember(2)]
        public int PortRemote;
        [ProtoMember(3)]
        public List<EndpointTransferMessage> LocalEndpoints { get; set; } = new List<EndpointTransferMessage>();


    }

    internal class ServerHolepunchState
    {
        public Action OnComplete;
        static RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();

        private SecureProtoRelayServer Server;
        private readonly Guid stateId;
        private readonly Guid requesterId;
        private readonly Guid destinationId;
        private bool requesterPunchSucess;
        private bool destinationPunchSucess;
        private IPEndPoint RequesterEndpoint;
        private EndpointTransferMessage RequesterLocalEndpoints;
        private IPEndPoint DestinationEndpoint;
        private EndpointTransferMessage DestinationLocalEndpoints;



        private bool IsWaitingUdpMessage;
        private object locker = new object();

        public ServerHolepunchState(SecureProtoRelayServer server, Guid stateId, Guid requesterId, Guid destinationId, bool encrypted = true)
        {
            Server = server;
            this.stateId = stateId;
            this.requesterId = requesterId;
            this.destinationId = destinationId;
            StartLifetimeCounter(5000);
            MiniLogger.Log(MiniLogger.LogLevel.Info, "------ Server State Encyrption enabled : " + encrypted.ToString());
            CreateChannels(encrypted);
        }
        
        private async void StartLifetimeCounter(int lifeSpanMs)
        {
            await Task.Delay(lifeSpanMs).ConfigureAwait(false);
            OnComplete?.Invoke();
            OnComplete = null;
        }

        public void HandleMessage(MessageEnvelope msg)
        {
            MiniLogger.Log(MiniLogger.LogLevel.Info, "server hp msg handled");

            if (msg.Header == HolepunchHeaders.SuccesAck)
                HanldePunchAck(msg);
        }

        public void HandleUdpMsg(EndPoint ep, MessageEnvelope e)
        {
            MiniLogger.Log(MiniLogger.LogLevel.Info, "server hp udpmsg handled");
            HandleEndpointRegisteryMesssage(ep, e);
        }

        // 1. An initiation message comes here
        // both initiator and destination receives a udp socket creation msg.
        public void CreateChannels(bool encrypted)
        {
            MiniLogger.Log(MiniLogger.LogLevel.Info, "server hp cmd create chhannel encyrption : " +encrypted.ToString());

            var channelCreationMsgRequester = new ChanneCreationMessage()
            {
                SharedSecret = Server.ServerUdpInitKey,
                RegistrationId = stateId,
                DestinationId= destinationId,
                Encrypted= encrypted
            };
            var channelCreationMsgDestination = new ChanneCreationMessage()
            {
                SharedSecret = Server.ServerUdpInitKey,
                RegistrationId = stateId,
                DestinationId= requesterId,
                Encrypted= encrypted
            };

            var envelope = GetEnvelope(HolepunchHeaders.CreateChannel);

            IsWaitingUdpMessage = true;
            BeginTimeoutCounter(500);

            Server.SendAsyncMessage(requesterId, envelope, channelCreationMsgRequester);
            Server.SendAsyncMessage(destinationId, envelope, channelCreationMsgDestination);
        }


        // 2.After initiation they both should send a udp msg, so that the server can retrieve their remote endpoints. 
        // if package drops we retry with timeout few times.
        public void HandleEndpointRegisteryMesssage(EndPoint ep, MessageEnvelope envelope)
        {
            MiniLogger.Log(MiniLogger.LogLevel.Info, "server hp ep registered");

            lock (locker)
            {
                if (!IsWaitingUdpMessage)
                    return;

                if (envelope.From == requesterId)
                {
                    RequesterEndpoint = ep as IPEndPoint;
                    RequesterLocalEndpoints =  envelope.UnpackPayload<EndpointTransferMessage>();
                }
                else if(envelope.From == destinationId)
                {
                    DestinationEndpoint = ep as IPEndPoint;
                    DestinationLocalEndpoints = envelope.UnpackPayload<EndpointTransferMessage>();
                }

                if (RequesterEndpoint != null && DestinationEndpoint != null)
                {
                    MiniLogger.Log(MiniLogger.LogLevel.Info, "requester port:" + RequesterEndpoint.Port);
                    MiniLogger.Log(MiniLogger.LogLevel.Info, "target port:" + DestinationEndpoint.Port);
                    IsWaitingUdpMessage = false;
                    SendStartPunchingMessage();
                }
            }
        }

        // this will ask for udp resend 3 times in case of package loss.
        private async void BeginTimeoutCounter(int timeout)
        {
            for (int i = 0; i < 3; i++)
            {
                await Task.Delay(timeout).ConfigureAwait(false);
                lock (locker)
                {
                    if (IsWaitingUdpMessage)
                        AskForUdpResend();
                }
            }
        }

        private void AskForUdpResend()
        {
            MiniLogger.Log(MiniLogger.LogLevel.Info, "$$ server hp udp resend");
            MessageEnvelope envelope = GetEnvelope(HolepunchHeaders.HoplePunchUdpResend);

            if (RequesterEndpoint == null)
            {
                envelope.MessageId = stateId;
                Server.SendAsyncMessage(requesterId, envelope);
            }
            if (DestinationEndpoint == null)
            {
                envelope.MessageId = stateId;
                Server.SendAsyncMessage(destinationId, envelope);
            }

        }

        // 3. Once we collected both endpoint both sides should receive
        // their target endpoints. Peers should start punching at that point.
        public void SendStartPunchingMessage()
        {
            MiniLogger.Log(MiniLogger.LogLevel.Info, "server hp start message ");

            MessageEnvelope envelope = GetEnvelope(HolepunchHeaders.HoplePunch);

            var epTransferMessageRequester = new EndpointTransferMessage()
            {
                IpRemote = DestinationEndpoint.Address.ToString(),
                PortRemote = DestinationEndpoint.Port,
                LocalEndpoints = DestinationLocalEndpoints.LocalEndpoints
            };
            var epTransferMessageDestination = new EndpointTransferMessage()
            {
                IpRemote = RequesterEndpoint.Address.ToString(),
                PortRemote = RequesterEndpoint.Port,
                LocalEndpoints = RequesterLocalEndpoints.LocalEndpoints
            };
            Server.SendAsyncMessage(destinationId, envelope, epTransferMessageDestination);
            Server.SendAsyncMessage(requesterId, envelope, epTransferMessageRequester);
        }

        // if both sides acks positive(both received msg from their udp holes) punch is sucessfull.
        // we also send their private Aes key here.
        // note that enire handsake is under an ssl client so its safe.
        EndpointTransferMessage msgRequesterSucces =  new EndpointTransferMessage();
        EndpointTransferMessage msgDestinationSucces =  new EndpointTransferMessage();
        int completed = 0;
        private void HanldePunchAck(MessageEnvelope m)
        {
            if (Interlocked.CompareExchange(ref completed, 0, 0) == 1)
                return;

            if (m.From == requesterId)
            {
                requesterPunchSucess = true;
                msgRequesterSucces = m.UnpackPayload<EndpointTransferMessage>();
            }
            else if(m.From == destinationId)
            {
                destinationPunchSucess = true;
                msgDestinationSucces = m.UnpackPayload<EndpointTransferMessage>();
            }

            if (requesterPunchSucess && destinationPunchSucess)
            {
                if(Interlocked.CompareExchange(ref completed, 1,0) == 1)
                    return;
                OnComplete?.Invoke();
                var mdst = GetEnvelope(HolepunchHeaders.SuccessFinalize);
                mdst.KeyValuePairs = new Dictionary<string, string>();
                mdst.KeyValuePairs["IP"] = msgRequesterSucces.IpRemote;
                mdst.KeyValuePairs["Port"] = msgRequesterSucces.PortRemote.ToString();

                var mreq = GetEnvelope(HolepunchHeaders.SuccessFinalize);
                mreq.KeyValuePairs = new Dictionary<string, string>();
                mreq.KeyValuePairs["IP"] = msgDestinationSucces.IpRemote;
                mreq.KeyValuePairs["Port"] = msgDestinationSucces.PortRemote.ToString();

                var sharedSecret = new byte[16];
                rng.GetNonZeroBytes(sharedSecret);
                mdst.Payload= sharedSecret;
                mreq.Payload= sharedSecret;

                Server.SendAsyncMessage(destinationId, mdst);
                Server.SendAsyncMessage(requesterId, mreq);
                OnComplete = null;
            }
        }

        private MessageEnvelope GetEnvelope(string header)
        {
            return new MessageEnvelope() 
            { Header = header, IsInternal = true, MessageId= stateId };

        }
    }
}
