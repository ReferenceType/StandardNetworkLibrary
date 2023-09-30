using MessagePackNetwork.Components;
using NetworkLibrary.TCP.Generic;

namespace MessagePackNetwork
{
    internal class PureMessagePackServer : GenericServer<MessagepackSerializer>
    {
        public PureMessagePackServer(int port, bool writeLenghtPrefix = true) : base(port, writeLenghtPrefix)
        {
        }
    }
}
