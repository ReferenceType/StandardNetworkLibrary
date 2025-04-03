using NetworkLibrary.DistributedP2P.Client;
using NetworkLibrary.DistributedP2P.Components;
using NetworkLibrary.DistributedP2P.SimpleRelay;
using NetworkLibrary.P2P.Components.HolePunch;
using NetworkLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkLibrary.DistributedP2P.Server.StateManagement
{
    class PipeData
    {
        public const int TokenLength = 32 + 24;//32 bytes signature, 24 bytes token
        public byte[] Token { get; set; }
        // localhost, localip, publicip
        public List<EndpointData> PipeEndpoints { get; set; } = new List<EndpointData>();
    }

    internal class ServerPipeState : ConversationStateBase
    {
        private PipeManager piper;
        private Guid from, to;
        private int ackCount = 0;
        private readonly IDistributedConnection connection;

        private string ChannelType;
        bool isTcpPipe = false;
        private string destinationDhPublicKey;
        private string requesterDhPublicKey;
        private ChannelInfo chInfo =  new ChannelInfo();
        public ServerPipeState(Guid stateId, IDistributedConnection connection, PipeManager piper) : base(stateId, 20000)
        {
            this.connection = connection;
            this.piper = piper;
        }

        /*
         * C1 wants pope req with C2
         * Server finds a suitible Relay to pipe on
         * Server obtains token from relay
         * Server sends conn info and token to C1 and C2
         * 
         * C2 then needs to verify C1 optionally, with this you know that token came from trusted server and client is who he says he is.
         * 
         */

        public override void HandleMessage(MessageEnvelope message)
        {
            switch (message.Header)
            {
                case InternalConstants.PipeRequestTcp:
                    HandlePipeRequest(message, true);
                    break;
                case InternalConstants.PipeRequestUdp:
                    HandlePipeRequest(message, false);
                    break;

                case InternalConstants.PipeReqAck://do nack also
                    HandlePipeReqAck(message);
                    break;

                case InternalConstants.ConnectionAckGood:
                    HandleGoodAck(message);
                    break;

                case InternalConstants.ConnectionAckBad:
                    HandleBadAck(message);
                    break;

            }
        }

        private void HandlePipeReqAck(MessageEnvelope message)
        {
            if (chInfo.RequiresKeyExchange())
                destinationDhPublicKey = message.KeyValuePairs["DH"];

            ObtainPipeToken().ContinueWith(HandlePipeToken);
        }

        private void HandlePipeRequest(MessageEnvelope message, bool tcp)
        {
            this.from = message.From;
            this.to = message.To;
            ChannelType = message.KeyValuePairs["Type"];

            chInfo.ChannelType = (ChannelType)int.Parse(message.KeyValuePairs["Type"]);
            chInfo.ChannelName = message.KeyValuePairs["Name"];

            if (chInfo.RequiresKeyExchange())
            {
                requesterDhPublicKey = message.KeyValuePairs["DH"];
                message.KeyValuePairs.Remove("DH");
            }

            connection.SendAsyncMessage(message);
            isTcpPipe = tcp;
        }

        private Task<PipeData> ObtainPipeToken()
        {
            //do only local for now
            byte[] token = piper.GetPipeToken(isTcpPipe);
            PipeData data = new PipeData();
            data.Token = token;
            data.PipeEndpoints = new List<EndpointData>() { new EndpointData("127.0.0.1", isTcpPipe ? 20011 : 20012) };


            return Task.FromResult(data);
        }


        private void HandlePipeToken(Task<PipeData> task)
        {
            var data = task.Result;
            var msg = CreateEnvelope();
            msg.Header = isTcpPipe ? InternalConstants.PipeTokenDeliveryTcp : InternalConstants.PipeTokenDeliveryUdp;
            var stream = SharerdMemoryStreamPool.RentStreamStatic();
            stream.Position32 = 0;

            KnownTypeSerializer.SerializePipeData(stream, data);
            msg.SetPayload(stream.GetBuffer(), 0, stream.Position32);
            msg.KeyValuePairs = new Dictionary<string, string>();

            if(chInfo.RequiresKeyExchange())
                msg.KeyValuePairs["DH"] = destinationDhPublicKey;

            connection.SendAsyncMessage(from, msg);

            if (chInfo.RequiresKeyExchange())
                msg.KeyValuePairs["DH"] = requesterDhPublicKey;

            connection.SendAsyncMessage(to, msg);

            SharerdMemoryStreamPool.ReturnStreamStatic(stream);
        }


        private void HandleBadAck(MessageEnvelope message)
        {
            lock (cancellationMutex)
            {
                var msg = CreateEnvelope();
                msg.Header = InternalConstants.ConnectionAckBad;
                connection.SendAsyncMessage(from, msg);
                connection.SendAsyncMessage(to, msg);
                Completed(false);
            }
        }

        private void HandleGoodAck(MessageEnvelope message)
        {
            if (Interlocked.Increment(ref ackCount) == 2)
            {
                lock (cancellationMutex)
                {
                    var msg = CreateEnvelope();
                    msg.Header = InternalConstants.ConnectionAckGood;
                    connection.SendAsyncMessage(from, msg);
                    connection.SendAsyncMessage(to, msg);
                    Completed(true);
                }
            }
        }

    }
}
