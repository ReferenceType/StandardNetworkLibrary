using NetworkLibrary.DistributedP2P.Client;
using NetworkLibrary.DistributedP2P.Components;
using NetworkLibrary.P2P.Components.HolePunch;
using NetworkLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace NetworkLibrary.DistributedP2P.Server.StateManagement
{
    internal class ServerUdpHolepunchState : ConversationStateBase
    {
        private readonly IDistributedConnection connection;
        private readonly SessionManager sessionManager;

        Guid From;
        Guid To;
        EndpointTransferMessage fromAdresses;
        byte[] fromPublicKey;

        EndpointTransferMessage toAddresses;
        byte[] toPublicKey;

        ChannelInfo info;
        private int succesCount;

        public ServerUdpHolepunchState(Guid stateId, IDistributedConnection connection, SessionManager sessionManager, ILogger logger) : base(stateId, 20000, logger)
        {
            this.connection = connection;
            this.sessionManager = sessionManager;
        }

        public override void HandleMessage(MessageEnvelope message)
        {
            switch (message.Header)
            {
                case InternalConstants.RequestHolepunchUdp:
                    HandleHolepunchRequest(message);
                    break;
                case InternalConstants.AckRequestHolepunchUdp:
                    HandleHolepunchRequestAck(message);
                    break;
                case InternalConstants.PunchSucces:
                    HandleSucces(message);
                    break;
                case InternalConstants.PunchFail:
                    HandleFailure(message);
                    break;
            }
        }

        // obtain port from destination endpoint
        private void HandleHolepunchRequest(MessageEnvelope message)
        {
            int offs = message.PayloadOffset;
            var hpData = KnownTypeSerializer.DeserializeHolepunchData(message.Payload, ref offs);

            info = hpData.ChannelInfo;
            fromPublicKey = hpData.DHPublic;
            fromAdresses = hpData.Endpoints;

            From = message.From;
            To = message.To;

            hpData.Endpoints = null;
            hpData.DHPublic = null;

            var stream = SharerdMemoryStreamPool.RentStreamStatic();

            KnownTypeSerializer.SerializeHolepunchData(stream, hpData);
            message.SetPayload(stream.GetBuffer(), 0, stream.Position32);
            connection.SendAsyncMessage(message);
            SharerdMemoryStreamPool.ReturnStreamStatic(stream);
        }

        private void HandleHolepunchRequestAck(MessageEnvelope message)
        {

            int offs = message.PayloadOffset;
            var hpData = KnownTypeSerializer.DeserializeHolepunchData(message.Payload, ref offs);
            toPublicKey = hpData.DHPublic;
            toAddresses = hpData.Endpoints;

            var msg = CreateEnvelope();
            msg.Header = InternalConstants.StartHP;
            msg.KeyValuePairs = new Dictionary<string, string>();
            msg.KeyValuePairs["Time"] = ((connection.GetTime() + 500) + (200*Math.Max(fromAdresses.LocalEndpoints.Count, toAddresses.LocalEndpoints.Count))).ToString();

            sessionManager.GetSessionData(From, out ServerSession sesFrom);
            sessionManager.GetSessionData(To, out ServerSession sesTo);



            if (sesFrom != null && sesTo != null)
            {
                if (IPHelper.IsZero(toAddresses.IpRemote))
                    toAddresses.IpRemote = sesTo.ClientPublicIp.Address.MapToIPv4().GetAddressBytes();
                if (IPHelper.IsZero(fromAdresses.IpRemote))
                    fromAdresses.IpRemote = sesFrom.ClientPublicIp.Address.MapToIPv4().GetAddressBytes();

                IPHelper.ObtainIpEndpoints(fromAdresses,
                                           toAddresses,
                                           out var FromNeedsToKnow,
                                           out var ToNeedsToKnow);


                var stream = SharerdMemoryStreamPool.RentStreamStatic();

                msg.To = From;
                hpData.Endpoints = FromNeedsToKnow;
                hpData.DHPublic = toPublicKey;
                KnownTypeSerializer.SerializeHolepunchData(stream, hpData);
                msg.SetPayload(stream.GetBuffer(), 0, stream.Position32);
                connection.SendAsyncMessage(msg);

                stream.Position32 = 0;

                msg.To = To;
                hpData.Endpoints = ToNeedsToKnow;
                hpData.DHPublic = fromPublicKey;
                KnownTypeSerializer.SerializeHolepunchData(stream, hpData);
                msg.SetPayload(stream.GetBuffer(), 0, stream.Position32);
                connection.SendAsyncMessage(msg);

                SharerdMemoryStreamPool.ReturnStreamStatic(stream);
            }
            else
            {
                Cancel();
            }

        }


        private void HandleFailure(MessageEnvelope message)
        {
            var msg = CreateEnvelope();
            msg.Header = InternalConstants.PunchFailAck;
            connection.SendAsyncMessage(From, msg);
            connection.SendAsyncMessage(To, msg);
            Completed(false);

        }

        private void HandleSucces(MessageEnvelope message)
        {

            var msg = CreateEnvelope();
            msg.Header = InternalConstants.PunchSuccesAck;
            msg.SetPayload(message.Payload, message.PayloadOffset, message.PayloadCount);
            if (message.From == From)
                connection.SendAsyncMessage(To, msg);
            else if(message.From == To)
                connection.SendAsyncMessage(From, msg);

            if (Interlocked.Increment(ref succesCount) == 2)
            {
                Completed(true);
            }
        }

    }
}
