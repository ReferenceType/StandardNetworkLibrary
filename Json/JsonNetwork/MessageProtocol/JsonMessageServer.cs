using JsonNetwork.Components;
using NetworkLibrary.MessageProtocol.Fast;

namespace JsonNetwork.MessageProtocol
{
    internal class JsonMessageServer : GenericMessageServerWrapper<JsonSerializer>
    {
        public JsonMessageServer(int port) : base(port)
        {
        }
    }
}
