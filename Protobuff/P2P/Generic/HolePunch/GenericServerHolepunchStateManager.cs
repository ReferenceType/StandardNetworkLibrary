//using MessageProtocol;
//using NetworkLibrary.MessageProtocol;
//using NetworkLibrary.Utils;
//using Protobuff.P2P.Generic.Interfaces.Messages;
//using Protobuff.P2P.HolePunch;
//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.Net;
//using System.Text;

//namespace Protobuff.P2P.Generic.HolePunch
//{
//    public class GenericServerHolepunchStateManager<E, ET, C,S,R>
//           where E : IMessageEnvelope, new()
//           where ET : IEndpointTransferMessage<ET>, new()
//           where C : IChanneCreationMessage, new()
//           where S : ISerializer, new()
//           where R : IRouterHeader, new()
//    {
//        readonly ConcurrentDictionary<Guid, GenericServerHolepunchState<E, ET, C, S, R>> activeStates
//           = new ConcurrentDictionary<Guid, GenericServerHolepunchState<E, ET, C, S, R>>();

//        // upon request on relay, this is called
//        public void CreateState(GenericRelayServer<E,ET,C,S,R> server, E message)
//        {
//            bool encrypted = true;
//            Guid stateId = message.MessageId;
//            if (message.KeyValuePairs != null)
//            {
//                if (message.KeyValuePairs.TryGetValue("Encrypted", out string value))
//                {
//                    if (bool.TryParse(value, out var isEncrypted))
//                    {
//                        encrypted = isEncrypted;
//                    }
//                }
//            }

//            MiniLogger.Log(MiniLogger.LogLevel.Info, "Server hp state created with id:" + stateId.ToString());
//            var state = new GenericServerHolepunchState<E, ET, C, S, R>(server, stateId, message.From, message.To, encrypted);
//            state.OnComplete += () => activeStates.TryRemove(stateId, out _);

//            activeStates.TryAdd(stateId, state);
//        }

//        public bool HandleMessage(E message)
//        {
//            if (activeStates.TryGetValue(message.MessageId, out var state))
//            {
//                state?.HandleMessage(message);
//                return true;
//            }
//            return false;
//        }

//        public bool HandleUdpMessage(EndPoint ep, E message)
//        {
//            if (activeStates.TryGetValue(message.MessageId, out var state))
//            {
//                state?.HandleUdpMsg(ep, message);
//                return true;
//            }
//            return false;
//        }
//    }
//}

