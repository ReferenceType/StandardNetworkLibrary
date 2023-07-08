using MessagePack;
using MessagePackNetwork.Components;
using NetworkLibrary.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessagePackNetwork.Pure
{
    internal class MessagePackServer : GenericServer<MessagepackSerializer>
    {
        public MessagePackServer(int port, bool writeLenghtPrefix = true) : base(port, writeLenghtPrefix)
        {
        }
    }
}
