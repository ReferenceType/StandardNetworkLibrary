using JsonMessageNetwork.Components;
using NetworkLibrary.TCP.Generic;

namespace JsonMessageNetwork
{
    public class PureJsonServer : GenericServer<JsonSerializer>
    {
        public PureJsonServer(int port, bool writeLenghtPrefix = true) : base(port, writeLenghtPrefix)
        {
        }
    }
}
