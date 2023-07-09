using MessagePackNetwork.Components;
using MessageProtocol;

namespace MessagePackNetwork
{
    internal class MessagePackMessageClientInternal : MessageClient<MessageEnvelope, MessagepackSerializer>
    {

    }
    internal class MessagePackMesssageServerInternal : MessageServer<MessageEnvelope, MessagepackSerializer>
    {
        public MessagePackMesssageServerInternal(int port) : base(port)
        {
        }

    }
}
