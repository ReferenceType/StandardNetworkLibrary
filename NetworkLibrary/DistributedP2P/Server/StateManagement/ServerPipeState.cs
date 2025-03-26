using NetworkLibrary.DistributedP2P.Components;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NetworkLibrary.DistributedP2P.Server.StateManagement
{
    internal class ServerPipeState : ConversationStateBase
    {
        public ServerPipeState(Guid stateId) : base(stateId)
        {
        }

        public override void HandleMessage(MessageEnvelope message)
        {
            throw new NotImplementedException();
        }
    }
}
