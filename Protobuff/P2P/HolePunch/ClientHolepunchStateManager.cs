using NetworkLibrary;
using NetworkLibrary.MessageProtocol;
using NetworkLibrary.MessageProtocol.Serialization;
using NetworkLibrary.Utils;
using Protobuff.P2P.StateManagemet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Protobuff.P2P.HolePunch
{
    internal class ClientHolepunchStateManager:StateManager
    {
        public ClientHolepunchStateManager(INetworkNode networkNode) : base(networkNode)
        {
        }

        public Task<ClientHolepunchState> CreateChannel(RelayClientBase client, MessageEnvelope message, int timeoutMS = 5000)
        {
            //var chmsg = message.UnpackPayload<ChanneCreationMessage>();
            var chmsg = KnownTypeSerializer.DeserializeChanneCreationMessage(message.Payload, message.PayloadOffset);

            var state = new ClientHolepunchState(client, message.MessageId, chmsg.DestinationId, timeoutMS, chmsg.Encrypted);
            var tcs =  new TaskCompletionSource<ClientHolepunchState>( TaskCreationOptions.RunContinuationsAsynchronously);
            state.Completed += (st) =>
            {
                tcs.TrySetResult(st as ClientHolepunchState);
            };
            AddState(state);
            state.HandleMessage(message);
            return tcs.Task;
          
          
            //try
            //{
            //    return await state.Completion.Task.ConfigureAwait(false) == null ? null : state;

            //}
            //finally
            //{
            //    TryRemoveState(message.MessageId, out _);
                
            //}
        }

        // Initiator calls this and generates the async state.
        // Trickey point is that the both sides will recieve create channel command. however initiater will handle it  on
        // handle message method because state is already registered.
        // where the remote peer will handle it on Create channel method.
        public Task<ClientHolepunchState> CreateHolepunchRequest(RelayClientBase client, Guid targetId, int timeOut, bool encrypted = true)
        {
            Guid stateId = Guid.NewGuid();
            MiniLogger.Log(MiniLogger.LogLevel.Info, client.sessionId.ToString() + " is Requested hp with state" + stateId.ToString());
            ClientHolepunchState state = new ClientHolepunchState(client, stateId, targetId, timeOut, encrypted);

            AddState(state);
            var tcs = new TaskCompletionSource<ClientHolepunchState>(TaskCreationOptions.RunContinuationsAsynchronously);
            state.Completed += (st) =>
            {
                tcs.TrySetResult(st as ClientHolepunchState);
            };
            var request = new MessageEnvelope()
            {
                Header = Constants.HolePunchRequest,
                IsInternal = true,
                MessageId = stateId,
                KeyValuePairs = new Dictionary<string, string>() { { "Encrypted", encrypted.ToString() } }

            };
            client.SendAsyncMessage(targetId, request);
            return tcs.Task;
            //try
            //{
            //    return await state.Completion.Task.ConfigureAwait(false) as ClientHolepunchState;
            //}
            //finally
            //{
            //    TryRemoveState(stateId, out _);
            //}
        }

    }
}
