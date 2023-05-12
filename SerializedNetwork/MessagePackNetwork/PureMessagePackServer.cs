using MessagePackNetwork.Components;
using NetworkLibrary.Generic;
using System;
using System.Collections.Generic;
using System.Text;

namespace MessagePackNetwork
{
    internal class PureMessagePackServer : GenericServer<MessagepackSerializer>
    {
        public PureMessagePackServer(int port, bool writeLenghtPrefix = true) : base(port, writeLenghtPrefix)
        {
        }
    }
}
