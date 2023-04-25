using JsonMessageNetwork.Components;
using MessageProtocol;
using System;
using System.Collections.Generic;
using System.Text;

namespace JsonMessageNetwork.Internal
{
    internal class JsonMessageServerInternal : MessageServer<MessageEnvelope, JsonSerializer>
    {
        public JsonMessageServerInternal(int port) : base(port)
        {
        }
    }
}
