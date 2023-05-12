using NetworkLibrary;
using NetworkLibrary.MessageProtocol.Serialization;
using NetworkLibrary.Utils;
using Protobuff.P2P.HolePunch;
using System;
using System.Collections.Concurrent;
using System.Net;

namespace Protobuff.P2P
{
    internal class ServerHolepunchStateManager
    {
        readonly ConcurrentDictionary<Guid, ServerHolepunchState> activeStates
           = new ConcurrentDictionary<Guid, ServerHolepunchState>();

        // upon request on relay, this is called
        public void CreateState(SecureProtoRelayServer server, MessageEnvelope message)
        {
            bool encrypted = true;
            Guid stateId = message.MessageId;
            if (message.KeyValuePairs != null)
            {
                if (message.KeyValuePairs.TryGetValue("Encrypted", out string value))
                {
                    if (bool.TryParse(value, out var e))
                    {
                        encrypted = e;
                    }
                }
                else
                {

                }
            }
#if DEBUG
            MiniLogger.Log(MiniLogger.LogLevel.Info, "Server hp state created with id:" + stateId.ToString());
#endif
            var state = new ServerHolepunchState(server, stateId, message.From, message.To, encrypted);
            state.OnComplete += () => activeStates.TryRemove(stateId, out _);

            activeStates.TryAdd(stateId, state);
        }

        public bool HandleMessage(MessageEnvelope message)
        {
            if (activeStates.TryGetValue(message.MessageId, out var state))
            {
                state?.HandleMessage(message);
                return true;
            }
            return false;
        }

        public bool HandleUdpMessage(EndPoint ep, MessageEnvelope message)
        {
            if (activeStates.TryGetValue(message.MessageId, out var state))
            {
                state?.HandleUdpMsg(ep, message);
                return true;
            }
            return false;
        }
    }
}
