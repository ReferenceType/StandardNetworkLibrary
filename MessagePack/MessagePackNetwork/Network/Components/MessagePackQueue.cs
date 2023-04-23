using MessageProtocol;
using System;
using System.Collections.Generic;
using System.Text;

namespace MessagePackNetwork.Network.Components
{
    internal class MessagePackQueue : GenericMessageQueue<MessagePackSerializer_, MessageEnvelope>
    {
        public MessagePackQueue(int maxIndexedMemory, bool writeLengthPrefix = true) : base(maxIndexedMemory, writeLengthPrefix)
        {
        }
    }
}
