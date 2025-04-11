using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NetworkLibrary.DistributedP2P.Components
{
    internal interface IDistributedConnection : ITimeProvider
    {
        void SendAsyncMessage(MessageEnvelope msgs);
        void SendAsyncMessage(Guid a, MessageEnvelope msgs);
        Task<MessageEnvelope> SendMessageAndWaitResponse(Guid a, MessageEnvelope msg);
        Task<MessageEnvelope> SendMessageAndWaitResponse(MessageEnvelope msg);
    }
}
