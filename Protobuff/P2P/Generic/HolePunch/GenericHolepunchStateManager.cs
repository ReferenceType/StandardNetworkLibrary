//using MessageProtocol;
//using NetworkLibrary;
//using NetworkLibrary.MessageProtocol;
//using NetworkLibrary.Utils;
//using Protobuff.P2P.Generic.Interfaces.Messages;
//using Protobuff.P2P.HolePunch;
//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.Text;
//using System.Threading.Tasks;

//namespace Protobuff.P2P.Generic.HolePunch
//{
//    public class GenericClientHolepunchStateManager<E,ET,C,S>
//           where E : IMessageEnvelope, new()
//           where ET : IEndpointTransferMessage<ET>, new()
//           where C : IChanneCreationMessage, new()
//           where S : ISerializer, new()
//    {
//        ConcurrentDictionary<Guid, GenericClientHolepunchState<E, ET, C, S>> clientHolepunchStates =
//            new ConcurrentDictionary<Guid, GenericClientHolepunchState<E, ET, C, S>>();
//        public bool HandleMessage(E message)
//        {
//            if (clientHolepunchStates.TryGetValue(message.MessageId, out var state))
//            {
//                state.HandleMessage(message);
//                return true;
//            }

//            return false;
//        }

//        public async Task<GenericClientHolepunchState<E, ET, C, S>> CreateChannel(GenericRelayClient<E, ET, C, S> client, E message, int timeoutMS = 5000)
//        {
//            var chmsg = message.UnpackPayload<C>();
//            var state = new GenericClientHolepunchState<E, ET, C, S>(client, message.MessageId, chmsg.DestinationId, timeoutMS, chmsg.Encrypted);
//            clientHolepunchStates.TryAdd(message.MessageId, state);
//            state.HandleMessage(message);
//            try
//            {
//                return await state.Completion.Task.ConfigureAwait(false) == null ? null : state;

//            }
//            finally
//            {
//                clientHolepunchStates.TryRemove(message.MessageId, out _);
//            }



//        }

//        // Initiator calls this and generates the async state.
//        // Trickey point is that the both sides will recieve create channel command. however initiater will handle it  on
//        // handle message method because state is already registered.
//        // where the remote peer will handle it on Create channel method.
//        public async Task<GenericSecureUdpMessageClient<E,S>> CreateHolepunchRequest(GenericRelayClient<E, ET, C, S> client, Guid targetId, int timeOut, bool encrypted = true)
//        {
//            Guid stateId = Guid.NewGuid();
//            MiniLogger.Log(MiniLogger.LogLevel.Info, client.sessionId.ToString() + " is Requested hp with state" + stateId.ToString());
//            GenericClientHolepunchState<E, ET, C, S> state = new GenericClientHolepunchState<E, ET, C, S>(client, stateId, targetId, timeOut, encrypted);
//            clientHolepunchStates.TryAdd(stateId, state);

//            var request = new MessageEnvelope()
//            {
//                Header = HolepunchHeaders.HolePunchRequest,
//                IsInternal = true,
//                MessageId = stateId,
//                KeyValuePairs = new Dictionary<string, string>() { { "Encrypted", encrypted.ToString() } }

//            };
//            client.SendAsyncMessage(targetId, request);
//            try
//            {
//                return await state.Completion.Task.ConfigureAwait(false);
//            }
//            finally
//            {
//                clientHolepunchStates.TryRemove(stateId, out _);
//            }
//        }
//    }
//}
