using NetworkLibrary.DistributedP2P.Client;
using NetworkLibrary.DistributedP2P.Components;
using NetworkLibrary.P2P.Components.HolePunch;
using NetworkLibrary.Utils;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkLibrary.DistributedP2P.Server.StateManagement
{
    class PipeData
    {
        public const int TokenLength = 32 + 24;//32 bytes signature, 24 bytes token
        public byte[] Token { get; set; }
        public byte[] DHPublic { get; set; }
        // localhost, localip, publicip
        public EndpointData PipeEndpoint { get; set; }
    }
    class PipeResult
    {
        public byte[] Token { get; set; }
        public EndpointData PipeEndpoint { get; set; }
        public bool IsSuccesfull { get; internal set; }
    }

    internal class ServerPipeState : ConversationStateBase
    {
        private Guid from, to;
        private int ackCount = 0;
        private readonly IServerConnection connection;

        private string ChannelType;
        bool isTcpPipe = false;
        private byte[] destinationsDhPublicKey;

        private ChannelInfo chInfo = new ChannelInfo();
        public ServerPipeState(Guid stateId, IServerConnection connection) : base(stateId, 20000)
        {
            this.connection = connection;
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
            try
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
                        HandleBadAck();
                        break;

                }

            }
            catch (Exception ex)
            {
                Log($"Exception occured on server pipe state: {ex.Message}\n{ex.StackTrace}");
                HandleBadAck();
            }


        }
        //[A]
        private void HandlePipeRequest(MessageEnvelope message, bool tcp)
        {
            this.from = message.From;
            this.to = message.To;

            int offs = message.PayloadOffset;
            var clientPipeData = KnownTypeSerializer.DeserializeClientPipeData(message.Payload, ref offs);

            chInfo = clientPipeData.ChannelInfo;

            connection.SendAsyncMessage(message);// dest will know dh token in this.
            isTcpPipe = tcp;
        }
        //[B]
        private void HandlePipeReqAck(MessageEnvelope message)
        {
            int offs = message.PayloadOffset;
            var clientPipeData = KnownTypeSerializer.DeserializeClientPipeData(message.Payload, ref offs);

            if (chInfo.RequiresKeyExchange())
                destinationsDhPublicKey = clientPipeData.DHPublic;// requester will now this on pipetoken

            connection.GetPipeToken(isTcpPipe, from, to, HandlePipeResult);
                
        }

        private void HandlePipeResult(PipeResult result)
        {
            if (result.IsSuccesfull)
            {
                var data = new PipeData
                {
                    Token = result.Token,
                    PipeEndpoint = result.PipeEndpoint
                };
                HandlePipeToken(data);
            }
            else
            {
                Log("Unable to obtain pipe token");
                HandleBadAck();
            }
        
        }

      
        private void HandlePipeToken(PipeData data)
        {
            var msg = CreateEnvelope();
            msg.Header = isTcpPipe ? InternalConstants.PipeTokenDeliveryTcp : InternalConstants.PipeTokenDeliveryUdp;

            var stream = SharerdMemoryStreamPool.RentStreamStatic();

            // destination already knows the public key
            KnownTypeSerializer.SerializePipeData(stream, data);
            msg.SetPayload(stream.GetBuffer(), 0, stream.Position32);
            connection.SendAsyncMessage(to, msg);

            // requester will know from this message
            data.DHPublic = destinationsDhPublicKey;
            stream.Position32 = 0;

            KnownTypeSerializer.SerializePipeData(stream, data);
            msg.SetPayload(stream.GetBuffer(), 0, stream.Position32);
            connection.SendAsyncMessage(from, msg);

            SharerdMemoryStreamPool.ReturnStreamStatic(stream);
        }


        private void HandleBadAck()
        {
            lock (cancellationMutex)
            {
                if (IsCompleted())
                    return;

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
                    if (IsCompleted())
                        return;

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
