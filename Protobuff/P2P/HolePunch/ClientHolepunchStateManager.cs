using MessageProtocol.Serialization;
using NetworkLibrary.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Protobuff.P2P.HolePunch
{
    internal class ClientHolepunchStateManager
    {
        ConcurrentDictionary<Guid, ClientHolepunchState> clientHolepunchStates =
            new ConcurrentDictionary<Guid, ClientHolepunchState>();
        public bool HandleMessage(MessageEnvelope message)
        {
            if (clientHolepunchStates.TryGetValue(message.MessageId, out var state))
            {
                state.HandleMessage(message);
                return true;
            }

            return false;
        }

        public async Task<ClientHolepunchState> CreateChannel(RelayClient client, MessageEnvelope message, int timeoutMS = 5000)
        {
            var chmsg = message.UnpackPayload<ChanneCreationMessage>();
            var state = new ClientHolepunchState(client, message.MessageId, chmsg.DestinationId, timeoutMS, chmsg.Encrypted);
            clientHolepunchStates.TryAdd(message.MessageId, state);
            state.HandleMessage(message);
            try
            {
                return await state.Completion.Task.ConfigureAwait(false) == null ? null : state;

            }
            finally
            {
                clientHolepunchStates.TryRemove(message.MessageId, out _);
            }



        }

        // Initiator calls this and generates the async state.
        // Trickey point is that the both sides will recieve create channel command. however initiater will handle it  on
        // handle message method because state is already registered.
        // where the remote peer will handle it on Create channel method.
        public async Task<EncryptedUdpProtoClient> CreateHolepunchRequest(RelayClient client, Guid targetId, int timeOut, bool encrypted = true)
        {
            Guid stateId = Guid.NewGuid();
            MiniLogger.Log(MiniLogger.LogLevel.Info, client.sessionId.ToString() + " is Requested hp with state" + stateId.ToString());
            ClientHolepunchState state = new ClientHolepunchState(client, stateId, targetId, timeOut, encrypted);
            clientHolepunchStates.TryAdd(stateId, state);

            var request = new MessageEnvelope()
            {
                Header = HolepunchHeaders.HolePunchRequest,
                IsInternal = true,
                MessageId = stateId,
                KeyValuePairs = new Dictionary<string, string>() { { "Encrypted", encrypted.ToString() } }

            };
            client.SendAsyncMessage(targetId, request);
            try
            {
                return await state.Completion.Task.ConfigureAwait(false);
            }
            finally
            {
                clientHolepunchStates.TryRemove(stateId, out _);
            }
        }

    }
}
