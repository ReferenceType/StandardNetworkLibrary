using NetworkLibrary.Components;
using NetworkLibrary.Utils;
using Protobuff.P2P.HolePunch;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using MessageProtocol;
using Protobuff.P2P.Generic.Interfaces.Messages;

namespace Protobuff.P2P.Generic.HolePunch
{
    public class GenericClientHolepunchState<E,ET,C,S>
           where E : IMessageEnvelope, new()
           where ET : IEndpointTransferMessage<ET>, new()
           where C : IChanneCreationMessage, new()
           where S : ISerializer, new()
    {
        public TaskCompletionSource<GenericSecureUdpMessageClient<E,S>> Completion
           = new TaskCompletionSource<GenericSecureUdpMessageClient<E, S>>(TaskCreationOptions.RunContinuationsAsynchronously);
        private int Success = 0;

        internal GenericSecureUdpMessageClient<E,S> holepunchClient;
        private GenericMessageSerializer<E,S> serializer = new GenericMessageSerializer<E, S>();

        private readonly GenericRelayClient<E, ET, C, S> client;
        internal readonly Guid stateId;
        internal readonly Guid DestinationId;
        internal bool encrypted = true;

        // initiator client is the one who generated the state id.
        // this id traveled though relay to here.
        public GenericClientHolepunchState(GenericRelayClient<E, ET, C, S> client, Guid stateId, Guid To, int timeoutMs = 5000, bool encrypted = true)
        {
            StartLifetimeCounter(timeoutMs);
            this.client = client;
            this.stateId = stateId;
            DestinationId = To;
            this.encrypted = encrypted;
            MiniLogger.Log(MiniLogger.LogLevel.Info, "---------- Encryption:  " + encrypted.ToString());
        }

        public void HandleMessage(E message)
        {
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
            await Task.Delay(lifeSpanMs).ConfigureAwait(false);
            if (!Completion.Task.IsCompleted)
            {
                Interlocked.Exchange(ref cancelSends, 1);
                Interlocked.Exchange(ref endReceives, 1);
                holepunchClient.Dispose();
                Completion.TrySetResult(null);
            }
        }

        private void CreateUdpChannel(E message)
        {
            MiniLogger.Log(MiniLogger.LogLevel.Info, "creating udp hp ch" + client.sessionId.ToString());

            var chMessage = message.UnpackPayload<ChanneCreationMessage>();
            var aesAlgorithm = new ConcurrentAesAlgorithm(chMessage.SharedSecret, chMessage.SharedSecret);

            holepunchClient = new GenericSecureUdpMessageClient<E, S>(aesAlgorithm);
            holepunchClient.SocketSendBufferSize = 12800000;
            holepunchClient.ReceiveBufferSize = 12800000;
            //holepunchClient.OnMessageReceived += HolePunchPeerMsgReceived;
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

            E envelope = GetEnvelope("");
            ET innerMsg = new ET();
            innerMsg.LocalEndpoints = GetLocalEndpoints();
            envelope.From = client.sessionId;

            holepunchClient.SendAsyncMessage(envelope, innerMsg);
        }
        private List<ET> GetLocalEndpoints()
        {
            List<ET> endpoints = new List<ET>();
            var lep = (IPEndPoint)holepunchClient.LocalEndpoint;

            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    if (ip.ToString() == "0.0.0.0")
                        continue;
                    endpoints.Add(new ET()
                    {
                        IpRemote = ip.ToString(),
                        PortRemote = lep.Port
                    });
                }
            }
            return endpoints;
        }
        int cancelSends;
        private void StartHolepunch(E message)
        {
            var endPoint = message.UnpackPayload<EndpointTransferMessage>();
            MiniLogger.Log(MiniLogger.LogLevel.Info, client.sessionId.ToString() + " --- punching  towards " + endPoint.IpRemote + " - " + endPoint.PortRemote);
            foreach (var item in endPoint.LocalEndpoints)
            {
                MiniLogger.Log(MiniLogger.LogLevel.Info, client.sessionId.ToString() + " --- punching  towards " + item.IpRemote + " - " + item.PortRemote);
            }

            message.From = client.sessionId;
            message.MessageId = stateId;

            var any = new IPEndPoint(IPAddress.Any, endPoint.PortRemote);
            holepunchClient.ReceiveOnceFrom(any, OnBytesReceived);

            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(endPoint.IpRemote).MapToIPv4(), endPoint.PortRemote);
            var bytes = serializer.SerializeMessageEnvelope(message, endPoint);

            PunchAlgorithm(bytes, 0, bytes.Length, ep);

            foreach (var endpointMsg in endPoint.LocalEndpoints)
            {
                IPEndPoint epl = new IPEndPoint(IPAddress.Parse(endpointMsg.IpRemote).MapToIPv4(), endpointMsg.PortRemote);
                var bytes_ = serializer.SerializeMessageEnvelope(message, endpointMsg);
                PunchAlgorithm(bytes_, 0, bytes_.Length, epl);
            }


        }
        private void PunchAlgorithm(byte[] bytes_, int offset, int count, EndPoint epl)
        {

            //message.Payload = null;

            // now we punch a hole through nat, this is experimentally optimized.
            if (Interlocked.CompareExchange(ref cancelSends, 0, 0) == 1)
                return;
            holepunchClient.SendTo(bytes_, 0, count, epl);

            if (Interlocked.CompareExchange(ref cancelSends, 0, 0) == 1)
                return;
            holepunchClient.SendTo(bytes_, 0, count, epl);

            if (Interlocked.CompareExchange(ref cancelSends, 0, 0) == 1)
                return;


            Task.Run(async () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    if (Interlocked.CompareExchange(ref cancelSends, 0, 0) == 1)
                        return;
                    holepunchClient.SendTo(bytes_, 0, count, epl);

                    await Task.Delay(i).ConfigureAwait(false);

                }
            });
        }
        ConcurrentDictionary<ET, bool> successes = new ConcurrentDictionary<ET, bool>();
        int endReceives = 0;
        int msgSent = 0;
        private void OnBytesReceived(byte[] arg1, int arg2, int arg3)
        {
            if (Interlocked.CompareExchange(ref endReceives, 0, 0) == 1)
                return;
            if (arg1 == null)
            {
                var any = new IPEndPoint(IPAddress.Any, 0);
                holepunchClient.ReceiveOnceFrom(any, OnBytesReceived);
                return;
            }

            if (Interlocked.CompareExchange(ref msgSent, 1, 0) == 1)
            {
                var any = new IPEndPoint(IPAddress.Any, 0);
                holepunchClient.ReceiveOnceFrom(any, OnBytesReceived);
                return;
            }

            E msg = serializer.DeserialiseEnvelopedMessage(arg1, 0, arg3);
            var succesfullEp = msg.UnpackPayload<ET>();
            if (successes.TryAdd(succesfullEp, true))
            {
                var envelope = GetEnvelope(HolepunchHeaders.SuccesAck);
                envelope.From = client.sessionId;
                envelope.MessageId = stateId;
                client.SendAsyncMessage(DestinationId, envelope, succesfullEp);
            }
            var any1 = new IPEndPoint(IPAddress.Any, 0);
            holepunchClient.ReceiveOnceFrom(any1, OnBytesReceived);
        }

        private void HolePunchPeerMsgReceived(MessageEnvelope obj)
        {
            if (Interlocked.CompareExchange(ref Success, 1, 0) == 0)
            {
                Interlocked.Exchange(ref cancelSends, 1);
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
        private void HandleSuccess(E message)
        {
            Interlocked.Exchange(ref cancelSends, 1);
            message.LockBytes();
            if (message.KeyValuePairs != null)
            {
                var ip = message.KeyValuePairs["IP"];
                var port = message.KeyValuePairs["Port"];
                ThreadPool.UnsafeQueueUserWorkItem(async (s) =>
                {

                    await Task.Delay(200);
                    Interlocked.Exchange(ref endReceives, 1);

                    if (encrypted)
                        holepunchClient.SwapAlgorith(new ConcurrentAesAlgorithm(message.Payload, message.Payload));
                    else
                        holepunchClient.SwapAlgorith(null);


                    holepunchClient.SetRemoteEnd(ip, int.Parse(port));

                    Completion.TrySetResult(holepunchClient);
                    MiniLogger.Log(MiniLogger.LogLevel.Info, "HP Complete!:" + ip);


                }, null);
                MiniLogger.Log(MiniLogger.LogLevel.Info, "Success Selected IP:" + ip);

            }
            else
            {
                holepunchClient.SwapAlgorith(new ConcurrentAesAlgorithm(message.Payload, message.Payload));
                // holepunchClient.SwapAlgorith(null);
                Completion.TrySetResult(holepunchClient);
            }

        }

        private E GetEnvelope(string Header)
        {
            return new E() { Header = Header, IsInternal = true, MessageId = stateId };
        }
    }

}
