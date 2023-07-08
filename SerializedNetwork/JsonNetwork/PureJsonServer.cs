using JsonMessageNetwork.Components;
using NetworkLibrary.Generic;

namespace JsonMessageNetwork
{
    public class PureJsonServer : GenericServer<JsonSerializer>
    {
        public PureJsonServer(int port, bool writeLenghtPrefix = true) : base(port, writeLenghtPrefix)
        {
        }
    }
}
