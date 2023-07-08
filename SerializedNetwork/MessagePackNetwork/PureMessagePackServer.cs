using MessagePackNetwork.Components;
using NetworkLibrary.Generic;

namespace MessagePackNetwork
{
    internal class PureMessagePackServer : GenericServer<MessagepackSerializer>
    {
        public PureMessagePackServer(int port, bool writeLenghtPrefix = true) : base(port, writeLenghtPrefix)
        {
        }
    }
}
