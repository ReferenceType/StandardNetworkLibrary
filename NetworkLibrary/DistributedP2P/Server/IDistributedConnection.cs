using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NetworkLibrary.DistributedP2P.Components;

namespace NetworkLibrary.DistributedP2P.Server
{
    internal interface IDistributedConnection : ITimeProvider
    {
      
        void SendAsyncMessage(MessageEnvelope msgs);
        void SendAsyncMessage(Guid a, MessageEnvelope msgs);
        Task<MessageEnvelope> SendMessageAndWaitResponse(Guid a, MessageEnvelope msg);
        Task<MessageEnvelope> SendMessageAndWaitResponse( MessageEnvelope msg);
    }
}
