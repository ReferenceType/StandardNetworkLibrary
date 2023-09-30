using JsonNetwork.Components;
using NetworkLibrary.TCP.Generic;

namespace JsonNetwork.Pure
{
    internal class JsonServer : GenericServer<JsonSerializer>
    {
        public JsonServer(int port, bool writeLenghtPrefix = true) : base(port, writeLenghtPrefix)
        {
        }
    }
}
