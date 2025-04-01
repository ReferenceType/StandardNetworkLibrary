using NetworkLibrary.DistributedP2P.Components;
using NetworkLibrary.DistributedP2P.Server;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NetworkLibrary.DistributedP2P.Client.StateManagement
{
    //Signed Challenge Tokens
    internal class ClientConnectionState : ConversationStateBase
    {
        private readonly IDistributedConnection connection;
        private readonly IClientDbConnection clientDbConnector;
        private readonly IClientAuthenticationToken authToken;


        private TaskCompletionSource<IConversationState> Completion = new TaskCompletionSource<IConversationState>(TaskCreationOptions.RunContinuationsAsynchronously);

        public Guid SessionId { get; private set; }

        public ClientConnectionState(Guid stateId, IDistributedConnection connection, IClientDbConnection clientDbConnector, IClientAuthenticationToken authToken):base(stateId)
        {
            this.connection = connection;
            this.clientDbConnector = clientDbConnector;
            this.authToken = authToken;
        }

        public override void HandleMessage(MessageEnvelope message)
        {
            switch (message.Header)
            {
             
                case InternalConstants.ConnectionGetClientPublicData:
                    SendClientPublicData(message);
                    break;
                case InternalConstants.ConnectionAckGood:
                    HandleConnectionSucces(message);
                    break;
                case InternalConstants.Error:
                    HandleConnectionFail(message);
                    break;
            }
        }

        public void Start()
        {
            MessageEnvelope msg = CreateEnvelope();
            msg.Header = InternalConstants.ConnectionReq;
            msg.KeyValuePairs = new Dictionary<string, string>();

            msg.KeyValuePairs.Add("AuthToken", authToken.Token);
            msg.KeyValuePairs.Add("AuthMethod", authToken.AuthenticationMethod);
            msg.KeyValuePairs.Add("AdditionalData", authToken.AdditionalData);

            connection.SendAsyncMessage(msg);
            // now server will authenticate after this
            // may ask additional data to link, if we are first timer
            // then succes or fail
        }

        private void SendClientPublicData(MessageEnvelope message)
        {
            MessageEnvelope response = CreateEnvelope();
            response.Header = InternalConstants.ConnectionAckClientPublicData;
            response.Payload = clientDbConnector.GetClientPublicData();

            connection.SendAsyncMessage(response);
        }


        private void HandleConnectionSucces(MessageEnvelope message)
        {
            SessionId = message.To;
            Completed(succes: true);
        }

        private void HandleConnectionFail(MessageEnvelope message)
        {
            if (message.KeyValuePairs.ContainsKey("Error"))
                ErrorMessage = message.KeyValuePairs["Error"];

            Completed(succes: false);
        }


    }
}
