using NetworkLibrary.Components;
using NetworkLibrary.P2P.Components.HolePunch;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;

namespace NetworkLibrary.P2P.Components.StateManagemet.Client
{
    internal class ClientHolepunchState : IState
    {
        public event Action<IState> Completed;
        public StateStatus Status => currentStatus;
        public Guid StateId { get; }
        internal Guid destinationId;
        internal IPEndPoint succesfulEpToReceive;
        internal IPEndPoint succesfullEpToSend;
        internal IPEndPoint relayServerEndpoint;
        internal Action Success;
        internal Action<byte[], List<EndpointData>> KeyReceived;
        internal EndpointTransferMessage targetEndpoints;
        private ConcurrentAesAlgorithm aesAlgorithm;
        internal byte[] cryptoKey;

        private bool encypted = true;
        private int stop;
        private bool isInitiator;
        private StateStatus currentStatus = StateStatus.Pending;
        private StateManager client;
        private int totalPunchRoutines = 0;
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        public ClientHolepunchState(Guid destinationId, Guid stateId, StateManager client, IPEndPoint relayServerEndpoint)
        {
            this.destinationId = destinationId;
            StateId = stateId;
            this.client = client;
            this.relayServerEndpoint = relayServerEndpoint;
        }
        // A asks B
        public void Initiate()
        {
            isInitiator = true;
            RetrieveRemoteEndpoint(destinationId);
        }
        public void InitiateByRemote(MessageEnvelope message)
        {
            HandleEndpointMessage(message);
        }
        // get remote ep then punch
        private void RetrieveRemoteEndpoint(Guid destinationId)
        {
            var message = new MessageEnvelope
            {
                IsInternal = true,
                Header = Constants.InitiateHolepunch,
                MessageId = StateId
            };
            if (encypted)
                message.KeyValuePairs = new Dictionary<string, string>() { { "Encrypted", null } };
            client.SendAsyncMessage(destinationId, message);
            // reply will be handled on HandleEndpointMessage
        }

        private void HandleEndpointMessage(MessageEnvelope message)
        {
            targetEndpoints = KnownTypeSerializer.DeserializeEndpointTransferMessage(message.Payload, message.PayloadOffset);

            // the remote ip on relay is local means relay server and remote client is in same local network
            var ipToVerify = targetEndpoints.LocalEndpoints.Last().Ip;
            if (IPUtils.IsLoopback(ipToVerify) || IPUtils.IsPrivate(ipToVerify))
            {
                // (localhost client + relay) vs remote client
                //if im not also local, i should try the internet address of the relay.
                var relayIp = relayServerEndpoint.Address.GetAddressBytes();
                if (!IPUtils.IsLoopback(relayIp) && !IPUtils.IsPrivate(relayIp))
                    targetEndpoints.LocalEndpoints.Add(new EndpointData(new IPEndPoint(relayServerEndpoint.Address, targetEndpoints.LocalEndpoints[0].Port)));
            }

            // Here we need to remove loopback because i might send messages to myself if we share the same port..
            List<EndpointData> toRemove = new List<EndpointData>();
            foreach (var endpoint in targetEndpoints.LocalEndpoints)
            {
                if (IPUtils.IsLoopback(endpoint.Ip))
                {
                    toRemove.Add(endpoint);
                }

            }
            foreach (var item in toRemove)
            {
                targetEndpoints.LocalEndpoints.Remove(item);
            }

            cryptoKey = targetEndpoints.IpRemote;// yea, didnt wanna change the type
            KeyReceived?.Invoke(cryptoKey, targetEndpoints.LocalEndpoints);
            if (cryptoKey != null)
            {
                aesAlgorithm = new ConcurrentAesAlgorithm(cryptoKey, cryptoKey);
            }
           
            var count = targetEndpoints.LocalEndpoints.Count;
            Interlocked.Exchange(ref totalPunchRoutines, count);
            for (int i = 0; i < count; i++)
            {
                var Ip = targetEndpoints.LocalEndpoints[i].Ip;
                var ep = targetEndpoints.LocalEndpoints[i].ToIpEndpoint();

                // first handle the local ips, start remote later, because if we are on local why go through internet..
                if (!IPUtils.IsPrivate(Ip))
                {
                    PunchRoutine(ep, delay: true);
                }
                else
                    PunchRoutine(ep);
            }

        }

        private async void PunchRoutine(IPEndPoint ep, bool delay = false)
        {
            try
            {
                var message = new MessageEnvelope() { MessageId = StateId };
                var endpointImShootingAt = new EndpointData() { Ip = ep.Address.GetAddressBytes(), Port = ep.Port };

                void Callback(PooledMemoryStream stream) => KnownTypeSerializer.SerializeEndpointData(stream, endpointImShootingAt);

                if (delay)
                {
                    await Task.Delay(1000, cancellationTokenSource.Token).ConfigureAwait(false);
                }
               // Console.WriteLine("Punching towards: " + ep.ToString());

                if (aesAlgorithm != null)
                    client.SendAsync(ep, message, Callback, aesAlgorithm);
                else
                    client.SendAsync(ep, message, Callback);

                for (int i = 0; i < 10; i++)
                {
                    if (Interlocked.CompareExchange(ref stop, 0, 0) == 1)
                        break;

                    if (aesAlgorithm != null)
                        client.SendAsync(ep, message, Callback, aesAlgorithm);
                    else
                        client.SendAsync(ep, message, Callback);

                    await Task.Delay(16 * i).ConfigureAwait(false);
                }
                await Task.Delay(100).ConfigureAwait(false);
            }
            catch { }
            finally
            {
                HolePunchRoutineExit();

            }


        }

        // inside of this message i must put the endpoint i shot at
        public void HandleMessage(IPEndPoint remoteEndpoint, MessageEnvelope message)
        {
            // here i would like to receive all, then rank the ips select nearest.
            if (Interlocked.CompareExchange(ref succesfulEpToReceive, remoteEndpoint, null) == null)
            {
                //Console.WriteLine("Received Sucess: " + remoteEndpoint.ToString());

                var msg = new MessageEnvelope()
                {
                    IsInternal = true,
                    Header = Constants.HolePunchSucces,
                    MessageId = StateId,
                };
                // payload is the endpoint where remote peer was shooting at.
                msg.SetPayload(message.Payload, message.PayloadOffset, message.PayloadCount);
                client.SendAsyncMessage(destinationId, msg);
                Consensus();
            }
        }

        public void HandleMessage(MessageEnvelope message)
        {
            if (message.Header == Constants.InitiateHolepunch)
            {
                HandleEndpointMessage(message);
            }
            // can go  now
            else if (message.Header == Constants.KeyTransfer)
            {
                //Interlocked.Exchange(ref stop, 1);
                message.LockBytes();
                cryptoKey = message.Payload;

            }
            else if (message.Header == Constants.HolePunchSucces)
            {
                if (Interlocked.Exchange(ref stop, 1) == 0)
                {
                    cancellationTokenSource.Cancel();
                    // get the sucessfull ep
                    succesfullEpToSend = KnownTypeSerializer.DeserializeEndpointData(message.Payload, message.PayloadOffset).ToIpEndpoint();
                    Consensus();

                }
            }
            else if (message.Header == Constants.HolpunchMessagesSent)
            {
                Consensus();
            }
            // here send hp finalize after all async udps are done

        }

        private void HolePunchRoutineExit()
        {
            if (Interlocked.Decrement(ref totalPunchRoutines) == 0)
            {
                // this means i have send all udp messages for holepunch
                client.SendAsyncMessage(destinationId,
                    new MessageEnvelope()
                    {
                        IsInternal = true,
                        Header = Constants.HolpunchMessagesSent,
                        MessageId = StateId
                    });
            }
        }

        int consensusState = 0;
        private int totalConsensus = 3;

        private void Consensus()
        {
            if (Interlocked.Increment(ref consensusState) == totalConsensus)
            {
                client.SendAsyncMessage(destinationId,
                   new MessageEnvelope()
                   {
                       IsInternal = true,
                       Header = Constants.NotifyServerHolepunch,
                       MessageId = StateId
                   });
                Release(true);
            }
        }

        private int isReleased = 0;

        public void Release(bool isCompletedSuccessfully)
        {
            if (Interlocked.CompareExchange(ref isReleased, 1, 0) == 0)
            {
                if (isCompletedSuccessfully && succesfullEpToSend != null && succesfulEpToReceive != null)
                    currentStatus = StateStatus.Completed;
                else
                    currentStatus = StateStatus.Failed;

                Completed?.Invoke(this);
                Completed = null;
            }
        }
    }
}
