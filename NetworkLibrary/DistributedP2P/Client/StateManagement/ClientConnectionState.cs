using NetworkLibrary.DistributedP2P.Components;
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

        private TaskCompletionSource<bool> timeSyncComplete = new TaskCompletionSource<bool>();

        public Guid SessionId { get; private set; }
        public int EDSPort { get; private set; }
        public ClientConnectionState(Guid stateId, IDistributedConnection connection, IClientDbConnection clientDbConnector, IClientAuthenticationToken authToken) : base(stateId, 20000)
        {
            this.connection = connection;
            this.clientDbConnector = clientDbConnector;
            this.authToken = authToken;
        }

        public override void HandleMessage(MessageEnvelope message)
        {
            switch (message.Header)
            {
                case InternalConstants.SyncTime:
                    SyncTime(message);
                    break;
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

        private async void SyncTime(MessageEnvelope message)
        {
            bool res = await timeSyncComplete.Task;
            if (res)
            {
                MessageEnvelope msg = CreateEnvelope();
                msg.Header = InternalConstants.SyncTime;
                connection.SendAsyncMessage(msg);
            }
        }

        private void SyncTime()
        {
            var task = connection.SyncTime().ContinueWith(t =>
            {
                timeSyncComplete.TrySetResult(true);
            });

        }

        public void Start()
        {
            MessageEnvelope msg = CreateEnvelope();
            msg.Header = InternalConstants.ConnectionReq;
            msg.KeyValuePairs = new Dictionary<string, string>();

            msg.KeyValuePairs.Add("AuthToken", authToken.Token);
            msg.KeyValuePairs.Add("AuthMethod", authToken.AuthenticationMethod);
            msg.KeyValuePairs.Add("AdditionalData", authToken.AdditionalData);

            List<string> locals = IPHelper.GetLocalIPAddresses4();
            int i = 0;
            foreach (var ip in locals)
            {
                msg.KeyValuePairs[ip] = null;
            }

            connection.SendAsyncMessage(msg);
            SyncTime();
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
            EDSPort = int.Parse(message.KeyValuePairs["EDSPort"]);
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
