using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkLibrary.DistributedP2P.Client
{
    internal interface IClientConnection
    {
        void SenAsyncMessage(MessageEnvelope message);
    }
}
