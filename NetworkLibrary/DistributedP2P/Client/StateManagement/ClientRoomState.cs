using NetworkLibrary.DistributedP2P.Components;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkLibrary.DistributedP2P.Client.StateManagement
{
    internal class ClientRoomState : ConversationStateBase
    {
        private readonly IDistributedConnection connection;

        public ClientRoomState(Guid stateId, int timeout,IDistributedConnection connection, ILogger logger) : base(stateId, timeout, logger)
        {
            this.connection = connection;
        }

        public void Start(string roomName,string passWord)
        {
            var msg = CreateEnvelope();
            msg.Header = InternalConstants.RequestCreateOrJointRoom;
            msg.KeyValuePairs = new Dictionary<string, string>();

            msg.KeyValuePairs["RoomName"] = roomName;
            msg.KeyValuePairs["Pass"] = passWord;

            connection.SendAsyncMessage(msg);
        }

        public override void HandleMessage(MessageEnvelope message)
        {
            switch (message.Header)
            {
                case InternalConstants.ResponseCreateOrJointRoom:
                    HandleResponse(message);
                     break;
            }
        }

        private void HandleResponse(MessageEnvelope message)
        {
            string succesStr = message.KeyValuePairs["Status"];
            bool success = bool.Parse(succesStr);
            if (success)
            {
                string token = message.KeyValuePairs["Token"];
                ExchangeToken(token);
            }
            else
            {
                Completed(false);
            }
          
        }

        private void ExchangeToken(string token)
        {
            TokenExchangeComplete();
        }

        private void TokenExchangeComplete()
        {
            var msg = CreateEnvelope();
            msg.Header = InternalConstants.JoinedRoom;
            connection.SendAsyncMessage(msg);
        }
    }
}
