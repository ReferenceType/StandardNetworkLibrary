using NetworkLibrary;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Protobuff.P2P.StateManagemet
{
    internal class ClientConnectionState : IState
    {
        public StateStatus Status => throw new NotImplementedException();

        public Guid StateId => throw new NotImplementedException();

        public event Action<IState> Completed;

        private TaskCompletionSource<MessageEnvelope> ServerUdpInitCommand =
                new TaskCompletionSource<MessageEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        private TaskCompletionSource<MessageEnvelope> ServerFinalization =
                   new TaskCompletionSource<MessageEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);


        public void HandleMessage(MessageEnvelope message)
        {
            throw new NotImplementedException();
        }

        public void HandleMessage(IPEndPoint remoteEndpoint, MessageEnvelope message)
        {
            throw new NotImplementedException();
        }

        public void Release(bool isCompletedSuccessfully)
        {
            throw new NotImplementedException();
        }
    }
}
