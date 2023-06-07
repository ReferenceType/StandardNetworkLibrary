using NetworkLibrary;
using NetworkLibrary.Components;
using NetworkLibrary.MessageProtocol;
using NetworkLibrary.MessageProtocol.Serialization;
using NetworkLibrary.Utils;
using Protobuff.P2P.StateManagemet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Protobuff.P2P.HolePunch
{
    internal class ClientHolepunchState:IState
    {
        public event Action<IState> Completed;
        public StateStatus Status { get; private set; }
        public Guid StateId { get; }
        internal readonly Guid DestinationId;
        internal EncryptedUdpProtoClient holepunchClient;
        internal bool encrypted = true;
        private int channelCreated;
        private readonly RelayClientBase client;

        // initiator client is the one who generated the state id.
        // this id traveled though relay to here.
        public ClientHolepunchState(RelayClientBase client, Guid stateId, Guid To, int timeoutMs = 5000, bool encrypted = true)
        {
            //StartLifetimeCounter(timeoutMs);
            this.client = client;
            this.StateId = stateId;
            DestinationId = To;
            this.encrypted = encrypted;
#if DEBUG

            MiniLogger.Log(MiniLogger.LogLevel.Info, "---------- Encryption:  " + encrypted.ToString());
#endif
        }

        public void HandleMessage(MessageEnvelope message)
        {
            //message.LockBytes();
            //ThreadPool.UnsafeQueueUserWorkItem((s) =>
            //{
                switch (message.Header)
                {
                    case Constants.CreateChannel:
                        CreateUdpChannel(message);
                        break;
                    case Constants.HoplePunchUdpResend:
                        SendUdpEndpointMessage();
                        break;
                    case Constants.HoplePunch:
                        StartHolepunch(message);
                        break;
                    case Constants.SuccessFinalize:
                        HandleSuccess(message);
                        break;
                }
         //   },null);
          
        }

        //private async void StartLifetimeCounter(int lifeSpanMs)
        //{
        //    await Task.Delay(lifeSpanMs).ConfigureAwait(false);
        //    if (!Completion.Task.IsCompleted)
        //    {
        //        Interlocked.Exchange(ref cancelSends, 1);
        //        Interlocked.Exchange(ref endReceives, 1);
        //        holepunchClient.Dispose();
        //        Completion.TrySetResult(null);
        //    }
        //}

        private void CreateUdpChannel(MessageEnvelope message)
        {
#if DEBUG
            MiniLogger.Log(MiniLogger.LogLevel.Info, "creating udp hp ch" + client.sessionId.ToString());
#endif
            //  var chMessage = message.UnpackPayload<ChanneCreationMessage>();
            var chMessage = KnownTypeSerializer.DeserializeChanneCreationMessage(message.Payload, message.PayloadOffset);
            var aesAlgorithm = new ConcurrentAesAlgorithm(chMessage.SharedSecret, chMessage.SharedSecret);

            holepunchClient = new EncryptedUdpProtoClient(aesAlgorithm);
            holepunchClient.SocketSendBufferSize = 12800000;
            holepunchClient.ReceiveBufferSize = 12800000;
            holepunchClient.Bind();

            // tricky point: its disaster when udp client receives from 2 endpoints.. corruption
            // dont receive from relay server only send.
            holepunchClient.SetRemoteEnd(client.connectHost, client.connectPort, receive: false);
            SendUdpEndpointMessage();
            Interlocked.Exchange(ref channelCreated, 1);
#if DEBUG
            MiniLogger.Log(MiniLogger.LogLevel.Info, "created udp hp channel" + client.sessionId.ToString());
#endif
        }

        private void SendUdpEndpointMessage()
        {
#if Debug
            MiniLogger.Log(MiniLogger.LogLevel.Info, "Sending Endpoint");
#endif
            if (Interlocked.CompareExchange(ref channelCreated,0,0)==0)
                return;
            MessageEnvelope envelope = GetEnvelope("");
            EndpointTransferMessage innerMsg = new EndpointTransferMessage();
            innerMsg.LocalEndpoints = GetLocalEndpoints();
            envelope.From = client.sessionId;
            
            holepunchClient.SendAsyncMessage(envelope, 
                (stream) => KnownTypeSerializer.SerializeEndpointTransferMessage(stream, innerMsg));
        }
        
        private List<EndpointData> GetLocalEndpoints()
        {
            List<EndpointData> endpoints = new List<EndpointData>();
            var lep = (IPEndPoint)holepunchClient.LocalEndpoint;

            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    if (ip.ToString() == "0.0.0.0")
                        continue;
                    endpoints.Add(new EndpointData()
                    {
                        Ip = ip.GetAddressBytes(),
                        Port = lep.Port
                    });
                }
            }
            return endpoints;
        }

        int cancelSends;
        ConcurrentProtoSerialiser seri = new ConcurrentProtoSerialiser();
        private void StartHolepunch(MessageEnvelope message)
        {
            var endPoint = KnownTypeSerializer.DeserializeEndpointTransferMessage(message.Payload, message.PayloadOffset);
#if DEBUG
            MiniLogger.Log(MiniLogger.LogLevel.Info, client.sessionId.ToString() + " --- punching  towards " + endPoint.IpRemote + " - " + endPoint.PortRemote);
            foreach (var item in endPoint.LocalEndpoints)
            {
                MiniLogger.Log(MiniLogger.LogLevel.Info, client.sessionId.ToString() + " --- punching  towards " + item.Ip + " - " + item.Port);
            }
#endif
            message.From = client.sessionId;
            message.MessageId = StateId;

            var any = new IPEndPoint(IPAddress.Any, endPoint.PortRemote);
            holepunchClient.ReceiveOnceFrom(any, OnBytesReceived);

            IPEndPoint ep = new IPEndPoint(new IPAddress(endPoint.IpRemote).MapToIPv4(), endPoint.PortRemote);

            var stream = SharerdMemoryStreamPool.RentStreamStatic();
            KnownTypeSerializer.SerializeEndpointData(stream, new EndpointData() { Ip = endPoint.IpRemote,Port = endPoint.PortRemote});
            var bytes = seri.EnvelopeMessageWithBytes(message, stream.GetBuffer(),0,stream.Position32);

            PunchAlgorithm(bytes, 0, bytes.Length, ep);

            foreach (var endpointMsg in endPoint.LocalEndpoints)
            {
                IPEndPoint epl = new IPEndPoint(new IPAddress(endpointMsg.Ip).MapToIPv4(), endpointMsg.Port);

                stream.Clear();
                KnownTypeSerializer.SerializeEndpointData(stream,endpointMsg);
                var bytes_ = seri.EnvelopeMessageWithBytes(message, stream.GetBuffer(), 0, stream.Position32);

                PunchAlgorithm(bytes_, 0, bytes_.Length, epl);
            }
            SharerdMemoryStreamPool.ReturnStreamStatic(stream);


        }
        private void PunchAlgorithm(byte[] bytes_, int offset, int count, EndPoint epl)
        {
            // now we punch a hole through nat, this is experimentally optimized.
            if (Interlocked.CompareExchange(ref cancelSends, 0, 0) == 1)
                return;
            else
            {
                holepunchClient.SendTo(bytes_, 0, count, epl);
                holepunchClient.SendTo(bytes_, 0, count, epl);
            }

            Task.Run(async () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        if (Interlocked.CompareExchange(ref cancelSends, 0, 0) == 1)
                            return;
                        holepunchClient.SendTo(bytes_, 0, count, epl);

                        await Task.Delay(i).ConfigureAwait(false);
                    } catch { }
                }
            });
        }

        ConcurrentDictionary<EndpointData, bool> successfullEndpoints = new ConcurrentDictionary<EndpointData, bool>();
        int endReceives = 0;
        int msgSent = 0;
        private void OnBytesReceived(byte[] arg1, int arg2, int arg3)
        {
            if (Interlocked.CompareExchange(ref endReceives, 0, 0) == 1)
                return;
            try
            {
                HandleUdpTemporaryReceived(arg1, arg2, arg3);
            }
            catch { }
           
        }
        private void HandleUdpTemporaryReceived(byte[] arg1, int arg2, int arg3)
        {
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

            MessageEnvelope msg = seri.DeserialiseEnvelopedMessage(arg1, 0, arg3);
            var succesfullEp = KnownTypeSerializer.DeserializeEndpointData(msg.Payload, msg.PayloadOffset);

            if (successfullEndpoints.TryAdd(succesfullEp, true))
            {
#if DEBUG
                MiniLogger.Log(MiniLogger.LogLevel.Info, "=============+++++++++==============Succes sending " + client.sessionId);
#endif
                var envelope = GetEnvelope(Constants.SuccesAck);
                envelope.MessageId = StateId;
                envelope.IsInternal = true;

                client.SendAsyncMessage(DestinationId, envelope,
                    (stream) => { KnownTypeSerializer.SerializeEndpointData(stream, succesfullEp); });

            }
            var any1 = new IPEndPoint(IPAddress.Any, 0);
            holepunchClient.ReceiveOnceFrom(any1, OnBytesReceived);
        }
      

        // done.
        private void HandleSuccess(MessageEnvelope message)
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

                    //Completion.TrySetResult(this);
                    Release(true);
#if DEBUG
                    MiniLogger.Log(MiniLogger.LogLevel.Info, "HP Complete!:" + ip);
#endif
                }, null);
#if DEBUG
                MiniLogger.Log(MiniLogger.LogLevel.Info, "Success Selected IP:" + ip);
#endif

            }
            else
            {
                holepunchClient.SwapAlgorith(new ConcurrentAesAlgorithm(message.Payload, message.Payload));
                // holepunchClient.SwapAlgorith(null);
                Release(true);
                //Completion.TrySetResult(this);
            }

        }

        private MessageEnvelope GetEnvelope(string Header)
        {
            return new MessageEnvelope() { Header = Header, IsInternal = true, MessageId = StateId };
        }

        public void HandleMessage(IPEndPoint remoteEndpoint, MessageEnvelope message)
        {
           
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
