using JsonMessageNetwork.Components;
using MessageProtocol;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace JsonMessageNetwork.Internal
{
    internal class JsonMessageSessionInternal : MessageSession<MessageEnvelope, JsonSerializer>
    {
        public JsonMessageSessionInternal(SocketAsyncEventArgs acceptedArg, Guid sessionId) : base(acceptedArg, sessionId)
        {
        }
    }
}
