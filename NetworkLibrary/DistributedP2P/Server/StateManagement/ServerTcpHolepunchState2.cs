using NetworkLibrary.DistributedP2P.Client;
using NetworkLibrary.DistributedP2P.Components;
using NetworkLibrary.P2P.Components.HolePunch;
using NetworkLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace NetworkLibrary.DistributedP2P.Server.StateManagement
{
    internal class ServerTcpHolepunchState2 : ConversationStateBase
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

        public ServerTcpHolepunchState2(Guid stateId, IDistributedConnection connection, SessionManager sessionManager) : base(stateId, 20000)
        {
            this.connection = connection;
            this.sessionManager = sessionManager;
        }

        public override void HandleMessage(MessageEnvelope message)
        {
            switch (message.Header)
            {
                case InternalConstants.RequestSimultaneousHolepunchTcp:
                    HandleHolepunchRequest(message);
                    break;
                case InternalConstants.AckRequestHolepunchTcp:
                    HandleHolepunchRequestAck(message);
                    break;
                case InternalConstants.PunchSucces:
                    HandleSucces(message);
                    break;

                case InternalConstants.PunchSwap:
                    RelaySwapMsg(message);
                    break;
                case InternalConstants.PunchFail:
                    HandleFailure(message);
                    break;
            }
        }

        private void RelaySwapMsg(MessageEnvelope message)
        {
            if (message.From == From)
            {
                connection.SendAsyncMessage(To, message);
            }
            else
            {
                connection.SendAsyncMessage(From, message);
            }
        }

        // obtain port from destination endpoint
        private void HandleHolepunchRequest(MessageEnvelope message)
        {
            info = new ChannelInfo();
            info.ChannelType = (ChannelType)int.Parse(message.KeyValuePairs["Type"]);
            info.ChannelName = message.KeyValuePairs["Name"];

            From = message.From;
            To = message.To;
            fromPort = int.Parse(message.KeyValuePairs["Port"]);
            if (info.RequiresKeyExchange())
                fromPublicKey = message.KeyValuePairs["DH"];
            connection.SendAsyncMessage(message);
        }

        private void HandleHolepunchRequestAck(MessageEnvelope message)
        {
            toPort = int.Parse(message.KeyValuePairs["Port"]);
            if (info.RequiresKeyExchange())
                toPublicKey = message.KeyValuePairs["DH"];




            var msg = CreateEnvelope();
            msg.Header = InternalConstants.StartHP;
            msg.KeyValuePairs = new Dictionary<string, string>();

            sessionManager.GetSessionData(From, out ServerSession sesFrom);
            sessionManager.GetSessionData(To, out ServerSession sesTo);

            if (sesFrom != null && sesTo != null)
            {
                IPHelper.ObtainIpEndpoints(fromPort, toPort, sesFrom, sesTo, out var FromNeedsToKnow, out var ToNeedsToKnow);

                // coordination signal
                double startTime = connection.GetTime();
                startTime += 1000 * (1 + Math.Max(FromNeedsToKnow.LocalEndpoints.Count, ToNeedsToKnow.LocalEndpoints.Count));
                msg.KeyValuePairs["Time"] = startTime.ToString(CultureInfo.InvariantCulture);

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

        protected override void Completed(bool succes)
        {
            Console.WriteLine("Server Finalized");
            base.Completed(succes);
        }


        private void HandleFailure(MessageEnvelope message)
        {
            var msg = CreateEnvelope();
            msg.Header = InternalConstants.PunchFailAck;
            connection.SendAsyncMessage(From, msg);
            connection.SendAsyncMessage(To, msg);
            Completed(false);

        }
        int succCounter = 0;
        private void HandleSucces(MessageEnvelope message)
        {
            if (Interlocked.Increment(ref succCounter) == 1)
            {
                var sts = message.KeyValuePairs["Status"];

                var msg = CreateEnvelope();
                msg.Header = InternalConstants.PunchSuccesAck;
                msg.KeyValuePairs = new Dictionary<string, string>();
                msg.SetPayload(message.Payload, message.PayloadOffset, message.PayloadCount);

                if (sts == "Accepted")
                {

                    if (message.From == From)
                    {
                        msg.KeyValuePairs["Use"] = "Connected";
                        connection.SendAsyncMessage(To, msg);

                        msg.KeyValuePairs["Use"] = "Accepted";
                        connection.SendAsyncMessage(From, msg);
                    }
                    else if (message.From == To)
                    {
                        msg.KeyValuePairs["Use"] = "Connected";
                        connection.SendAsyncMessage(From, msg);

                        msg.KeyValuePairs["Use"] = "Accepted";
                        connection.SendAsyncMessage(To, msg);
                    }

                }
                else // connected
                {

                    if (message.From == From)
                    {
                        msg.KeyValuePairs["Use"] = "Accepted";
                        connection.SendAsyncMessage(To, msg);

                        msg.KeyValuePairs["Use"] = "Connected";
                        connection.SendAsyncMessage(From, msg);
                    }
                    else if (message.From == To)
                    {
                        msg.KeyValuePairs["Use"] = "Accepted";
                        connection.SendAsyncMessage(From, msg);

                        msg.KeyValuePairs["Use"] = "Connected";
                        connection.SendAsyncMessage(To, msg);
                    }
                }
                Completed(true);

            }

        }

    }
}
