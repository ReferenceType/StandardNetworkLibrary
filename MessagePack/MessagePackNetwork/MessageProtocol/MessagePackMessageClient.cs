using MessagePack;
using MessagePackNetwork.Components;
using NetworkLibrary.MessageProtocol.Fast;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessagePackNetwork.MessageProtocol
{
    internal class MessagePackMessageClient:GenericMessageClientWrapper<MessagepackSerializer>
    {
    }
}
