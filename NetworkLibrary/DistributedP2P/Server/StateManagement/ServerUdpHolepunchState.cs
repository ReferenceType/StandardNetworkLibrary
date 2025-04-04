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
        int fromPort;
        string fromPublicKey;
        int toPort;
        string toPublicKey;
        ChannelInfo info;
        private int succesCount;

        public ServerUdpHolepunchState(Guid stateId, IDistributedConnection connection, SessionManager sessionManager) : base(stateId, 20000)
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
            info =  new ChannelInfo();
            info.ChannelType = (ChannelType)int.Parse(message.KeyValuePairs["Type"]);
            info.ChannelName = message.KeyValuePairs["Name"];

            From = message.From;
            To = message.To;
            fromPort = int.Parse(message.KeyValuePairs["Port"]);
            if(info.RequiresKeyExchange())
                fromPublicKey = message.KeyValuePairs["DH"];
            connection.SendAsyncMessage(message);
        }

        private void HandleHolepunchRequestAck(MessageEnvelope message)
        {
            toPort = int.Parse(message.KeyValuePairs["Port"]);
            if (info.RequiresKeyExchange())
                toPublicKey = message.KeyValuePairs["DH"];

            //signal
            double startTime = connection.GetTime();
            startTime += 500;


            var msg = CreateEnvelope();
            msg.Header = InternalConstants.StartHPUdp;
            msg.KeyValuePairs = new Dictionary<string, string>();
            msg.KeyValuePairs["Time"] = startTime.ToString(CultureInfo.InvariantCulture);

            sessionManager.GetSessionData(From, out ServerSession sesFrom);
            sessionManager.GetSessionData(To, out ServerSession sesTo);

            if (sesFrom != null && sesTo != null)
            {
                ObtainIpEndpoints(sesFrom, sesTo, out var FromNeedsToKnow, out var ToNeedsToKnow);

                var stream = SharerdMemoryStreamPool.RentStreamStatic();
                stream.Position32 = 0;

                KnownTypeSerializer.SerializeEndpointTransferMessage(stream, FromNeedsToKnow);
                msg.SetPayload(stream.GetBuffer(), 0, stream.Position32);
                msg.To = From;
                if (info.RequiresKeyExchange())
                    msg.KeyValuePairs["DH"] = toPublicKey;
                connection.SendAsyncMessage(msg);

                stream.Position32 = 0;

                KnownTypeSerializer.SerializeEndpointTransferMessage(stream, ToNeedsToKnow);
                msg.SetPayload(stream.GetBuffer(), 0, stream.Position32);
                msg.To = To;
                if (info.RequiresKeyExchange())
                    msg.KeyValuePairs["DH"] = fromPublicKey;
                connection.SendAsyncMessage(msg);
            }
            else
            {
                Cancel();
            }

        }


        private void ObtainIpEndpoints(ServerSession sesFrom, ServerSession sesTo, out EndpointTransferMessage FromNeedsToKnow, out EndpointTransferMessage ToNeedsToKnow)
        {
            FromNeedsToKnow = new EndpointTransferMessage();
            ToNeedsToKnow = new EndpointTransferMessage();

            IPHelper.ExtractLocalIpsWithMatchingSubnet(sesFrom.ClientLocalIps,
                                                       sesTo.ClientLocalIps,
                                                       out List<string> Locals_From_NeedsToKnow,
                                                       out List<string> Locals_To_NeedsToKnow);

            foreach (var local in Locals_From_NeedsToKnow)
            {
                EndpointData data = new EndpointData(local, toPort);
                FromNeedsToKnow.LocalEndpoints.Add(data);
            }

            foreach (var local in Locals_To_NeedsToKnow)
            {
                EndpointData data = new EndpointData(local, fromPort);
                ToNeedsToKnow.LocalEndpoints.Add(data);
            }

            // "From" is same network as the server.
            if (IPHelper.IsPrivateIPAddress(sesFrom.ClientPublicIp))
            {
                // "To" needs to get server adress to connect 
                // 0.0.0.0:0 means serverIp 
                ToNeedsToKnow.IpRemote = new byte[4];
                ToNeedsToKnow.PortRemote = fromPort;

            }
            else
            {
                // send just the publicIp of "From" to "To"
                ToNeedsToKnow.IpRemote = sesFrom.ClientPublicIp.Address.MapToIPv4().GetAddressBytes();
                ToNeedsToKnow.PortRemote = fromPort;
            }

            // "To" is same network as the server.
            if (IPHelper.IsPrivateIPAddress(sesTo.ClientPublicIp))
            {
                //"From" needs to get Server adress
                FromNeedsToKnow.IpRemote = new byte[4];
                FromNeedsToKnow.PortRemote = toPort;
            }
            else
            {
                // send just the public to "From"
                FromNeedsToKnow.IpRemote = sesTo.ClientPublicIp.Address.MapToIPv4().GetAddressBytes();
                FromNeedsToKnow.PortRemote = toPort;
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

            if(message.From == From)
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
