using NetworkLibrary;
using NetworkLibrary.MessageProtocol;
using NetworkLibrary.MessageProtocol.Serialization;
using NetworkLibrary.Utils;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Security.Cryptography;
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
    }

    internal class ServerHolepunchState
    {
        public Action OnComplete;
        static RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();

        private SecureProtoRelayServer Server;
        private readonly Guid stateId;
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
            this.stateId = stateId;
            this.requesterId = requesterId;
            this.destinationId = destinationId;
            StartLifetimeCounter(20000);
#if DEBUG

            MiniLogger.Log(MiniLogger.LogLevel.Info, "------ Server State Encyrption enabled : " + encrypted.ToString());
#endif
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
#if DEBUG

            MiniLogger.Log(MiniLogger.LogLevel.Info, "server hp msg handled: "+ msg.Header);
#endif

            if (msg.Header == HolepunchHeaders.SuccesAck)
                HanldePunchAck(msg);
        }

        public void HandleUdpMsg(EndPoint ep, MessageEnvelope e)
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
                RegistrationId = stateId,
                DestinationId = destinationId,
                Encrypted = encrypted
            };
            var channelCreationMsgDestination = new ChanneCreationMessage()
            {
                SharedSecret = Server.ServerUdpInitKey,
                RegistrationId = stateId,
                DestinationId = requesterId,
                Encrypted = encrypted
            };

            var envelope = GetEnvelope(HolepunchHeaders.CreateChannel);
            var envelope2 = GetEnvelope(HolepunchHeaders.CreateChannel);

            Interlocked.Exchange(ref IsWaitingUdpMessage, 1);
            BeginTimeoutCounter(500);

            //var stream = SharerdMemoryStreamPool.RentStreamStatic();
            //KnownTypeSerializer.SerializeChannelCreationMessage(stream, channelCreationMsgRequester);
            //envelope.SetPayload(stream.GetBuffer(), 0, stream.Position32);
            //Server.SendAsyncMessage(requesterId, envelope);

            //stream.Clear();
            //KnownTypeSerializer.SerializeChannelCreationMessage(stream, channelCreationMsgDestination);
            //envelope2.SetPayload(stream.GetBuffer(), 0, stream.Position32);
            //Server.SendAsyncMessage(destinationId, envelope2);

            //SharerdMemoryStreamPool.ReturnStreamStatic(stream);

            Server.SendAsyncMessage(requesterId, envelope,
                (stream)=> KnownTypeSerializer.SerializeChannelCreationMessage(stream, channelCreationMsgRequester));
            Server.SendAsyncMessage(destinationId, envelope2, 
                (stream) => KnownTypeSerializer.SerializeChannelCreationMessage(stream, channelCreationMsgDestination));

            //Server.SendAsyncMessage(requesterId, envelope, channelCreationMsgRequester);
            //Server.SendAsyncMessage(destinationId, envelope2, channelCreationMsgDestination);
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
                    //RequesterLocalEndpoints = envelope.UnpackPayload<EndpointTransferMessage>();
                    var rl = KnownTypeSerializer.DeserializeEndpointTransferMessage(envelope.Payload, envelope.PayloadOffset);
                    Interlocked.CompareExchange(ref RequesterLocalEndpoints, rl, null);
                }
               
            }
            else if (envelope.From == destinationId)
            {
                var de = ep as IPEndPoint;
                if(Interlocked.CompareExchange(ref DestinationEndpoint, de, null) == null)
                {
                    // DestinationLocalEndpoints = envelope.UnpackPayload<EndpointTransferMessage>();
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
#if DEBUG

            MiniLogger.Log(MiniLogger.LogLevel.Info, "server hp start message ");
#endif

            MessageEnvelope envelope = GetEnvelope(HolepunchHeaders.HoplePunch);
            MessageEnvelope envelope1 = GetEnvelope(HolepunchHeaders.HoplePunch);

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

            //var stream = SharerdMemoryStreamPool.RentStreamStatic();
            //KnownTypeSerializer.SerializeEndpointTransferMessage(stream, epTransferMessageDestination);
            //envelope.SetPayload(stream.GetBuffer(), 0, stream.Position32);

            //Server.SendAsyncMessage(destinationId, envelope);

            //stream.Clear();
            //KnownTypeSerializer.SerializeEndpointTransferMessage(stream, epTransferMessageRequester);
            //envelope.SetPayload(stream.GetBuffer(), 0, stream.Position32);

            //Server.SendAsyncMessage(requesterId, envelope);

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
                // msgRequesterSucces = m.UnpackPayload<EndpointTransferMessage>();
                var rs = KnownTypeSerializer.DeserializeEndpointData(m.Payload,m.PayloadOffset);
                Interlocked.Exchange(ref msgRequesterSucces, rs);
            }
            else if (m.From == destinationId)
            {
                if (Interlocked.CompareExchange(ref destinationPunchSucess, 1, 0) == 1) return;

                //msgDestinationSucces = m.UnpackPayload<EndpointTransferMessage>();
                var ds = KnownTypeSerializer.DeserializeEndpointData(m.Payload, m.PayloadOffset);
                Interlocked.Exchange(ref msgDestinationSucces, ds);

            }

            if (Interlocked.CompareExchange(ref requesterPunchSucess,1,1)==1 
                && Interlocked.CompareExchange(ref destinationPunchSucess, 1, 1) == 1)
            {
                if (Interlocked.CompareExchange(ref completed, 1, 0) == 1)
                    return;
                OnComplete?.Invoke();
                var mdst = GetEnvelope(HolepunchHeaders.SuccessFinalize);
                mdst.KeyValuePairs = new Dictionary<string, string>();
                mdst.KeyValuePairs["IP"] = new IPAddress( msgRequesterSucces.Ip).ToString();
                mdst.KeyValuePairs["Port"] = msgRequesterSucces.Port.ToString();

                var mreq = GetEnvelope(HolepunchHeaders.SuccessFinalize);
                mreq.KeyValuePairs = new Dictionary<string, string>();
                mreq.KeyValuePairs["IP"] = new IPAddress( msgDestinationSucces.Ip).ToString();
                mreq.KeyValuePairs["Port"] = msgDestinationSucces.Port.ToString();

                var sharedSecret = new byte[16];
                rng.GetNonZeroBytes(sharedSecret);
                mdst.Payload = sharedSecret;
                mreq.Payload = sharedSecret;

                Server.SendAsyncMessage(destinationId, mdst);
                Server.SendAsyncMessage(requesterId, mreq);
                OnComplete = null;
                MiniLogger.Log( MiniLogger.LogLevel.Info,$"Succecfully punched hole between {mdst.KeyValuePairs["IP"]} and {mreq.KeyValuePairs["IP"]}");
            }
        }

        private MessageEnvelope GetEnvelope(string header)
        {
            return new MessageEnvelope()
            { Header = header, IsInternal = true, MessageId = stateId };

        }
    }
}
