using NetworkLibrary.DistributedP2P.Components;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NetworkLibrary.DistributedP2P.Client.StateManagement
{
    internal class ClientPipeState : ConversationStateBase
    {


        public ClientPipeState(Guid stateId):base(stateId)
        {
           
        }

        public override void HandleMessage(MessageEnvelope message)
        {
            
        }

      
    }
}
