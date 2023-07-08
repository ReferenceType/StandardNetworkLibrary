using JsonMessageNetwork.Components;
using MessageProtocol;

namespace JsonMessageNetwork.Internal
{
    internal class ClientServer : MessageClient<MessageEnvelope, JsonSerializer>
    {
    }
    internal class JsonMessageServerInternal : MessageServer<MessageEnvelope, JsonSerializer>
    {
        public JsonMessageServerInternal(int port) : base(port)
        {
        }
    }
}
