using NetworkLibrary.Components;
using NetworkLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Protobuff.P2P.HolePunch
{
    internal class ClientHolepunchState
    {
        public TaskCompletionSource<EncryptedUdpProtoClient> Completion = new TaskCompletionSource<EncryptedUdpProtoClient>();
        private int Success = 0;

        internal EncryptedUdpProtoClient holepunchClient;
        private readonly RelayClient client;
        internal readonly Guid stateId;
        internal readonly Guid DestinationId;

        // initiator client is the one who generated the state id.
        // this id traveled though relay to here.
        public ClientHolepunchState(RelayClient client, Guid stateId, Guid To)
        {
            StartLifetimeCounter(5000);
            this.client = client;
            this.stateId = stateId;
            DestinationId = To;
        }

        public void HandleMessage(MessageEnvelope message)
        {
            MiniLogger.Log(MiniLogger.LogLevel.Info, "ClientStateGotMsg");
            switch (message.Header)
            {
                case HolepunchHeaders.CreateChannel:
                    CreateUdpChannel(message);
                    break;
                case HolepunchHeaders.HoplePunchUdpResend:
                    SendUdpEndpointMessage();
                    break;
                case HolepunchHeaders.HoplePunch:
                    StartHolepunch(message);
                    break;
                case HolepunchHeaders.SuccessFinalize:
                    HandleSuccess(message);
                    break;
            }
        }

        private async void StartLifetimeCounter(int lifeSpanMs)
        {
            await Task.Delay(lifeSpanMs);
            if (!Completion.Task.IsCompleted)
            {
                holepunchClient.Dispose();
                Completion.TrySetResult(null);
            }
        }

        private void CreateUdpChannel(MessageEnvelope message)
        {
            MiniLogger.Log(MiniLogger.LogLevel.Info, "creating udp hp ch" + client.sessionId.ToString());

            var chMessage = message.UnpackPayload<ChanneCreationMessage>();
            var aesAlgorithm = new ConcurrentAesAlgorithm(chMessage.SharedSecret, chMessage.SharedSecret);

            holepunchClient = new EncryptedUdpProtoClient(aesAlgorithm);
            holepunchClient.SocketSendBufferSize = 12800000;
            holepunchClient.ReceiveBufferSize = 12800000;
            holepunchClient.OnMessageReceived += HolePunchPeerMsgReceived;
            holepunchClient.Bind();

            // tricky point: its disaster when udp client receives from 2 endpoints.. corruption
            // dont receive from relay server only send.
            holepunchClient.SetRemoteEnd(client.connectHost, client.connectPort, receive: false);
            SendUdpEndpointMessage();

            MiniLogger.Log(MiniLogger.LogLevel.Info, "created udp hp channel" + client.sessionId.ToString());

        }

        private void SendUdpEndpointMessage()
        {
            MiniLogger.Log(MiniLogger.LogLevel.Info, "Sending Endpoint");

            MessageEnvelope envelope = GetEnvelope("");
            envelope.From = client.sessionId;
           
            holepunchClient.SendAsyncMessage(envelope);
        }

        int cancelSends;
        private void StartHolepunch(MessageEnvelope message)
        {
            var endPoint = message.UnpackPayload<EndpointTransferMessage>();
            MiniLogger.Log(MiniLogger.LogLevel.Info, client.sessionId.ToString() + " --- punching  towards " + endPoint.PortRemote);
           
            holepunchClient?.SetRemoteEnd(endPoint.IpRemote, endPoint.PortRemote);
            //message.Payload = null;

            // now we punch a hole through nat, this is exp[erimentally optimized.

            holepunchClient?.SendAsyncMessage(message);
            holepunchClient?.SendAsyncMessage(message);
            holepunchClient?.SendAsyncMessage(message);

            Task.Run(async () =>
            {
                for (int i = 0; i < 30; i++)
                {
                    if(Interlocked.CompareExchange(ref cancelSends,0,0)==1)
                        return;
                    holepunchClient?.SendAsyncMessage(message);
                    if (i > 10)
                        await Task.Delay(i - 10);

                }
            });
        }

        private void HolePunchPeerMsgReceived(MessageEnvelope obj)
        {
            if (Interlocked.CompareExchange(ref Success, 1, 0) == 0)
            {
                Interlocked.Increment(ref cancelSends);
                MiniLogger.Log(MiniLogger.LogLevel.Info, "got hp feedback yay!" + client.sessionId.ToString());
                SendAckToRelay();
            }
        }

        private void SendAckToRelay()
        {
            MiniLogger.Log(MiniLogger.LogLevel.Info, "sending ack to relay!!");
            var envelope = GetEnvelope(HolepunchHeaders.SuccesAck);
            client.SendAsyncMessage(DestinationId, envelope);
        }

        // done.
        private void HandleSuccess(MessageEnvelope message)
        {
            MiniLogger.Log(MiniLogger.LogLevel.Info, "success!!");
            message.LockBytes();
            holepunchClient.SwapAlgorith(new ConcurrentAesAlgorithm(message.Payload, message.Payload));
            //holepunchClient.Connect((IPEndPoint)holepunchClient.RemoteEndPoint);
            Completion.TrySetResult(holepunchClient);
        }

        private MessageEnvelope GetEnvelope(string Header)
        {
            return new MessageEnvelope() { Header = Header, IsInternal = true, MessageId = stateId };
        }
    }
}
