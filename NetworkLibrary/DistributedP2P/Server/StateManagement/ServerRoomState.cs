using NetworkLibrary.DistributedP2P.Components;
using System;

namespace NetworkLibrary.DistributedP2P.Server.StateManagement
{
    internal class ServerRoomState : ConversationStateBase
    {
        private readonly IServerConnection connection;

        public ServerRoomState(Guid stateId, int timeout, IServerConnection connection, ILogger logger) : base(stateId, timeout, logger)
        {
            this.connection = connection;
        }

        public override void HandleMessage(MessageEnvelope message)
        {
            switch (message.Header)
            {
                case InternalConstants.RequestCreateOrJointRoom:
                    HandleRoomCreation(message);
                    break;
                case InternalConstants.JoinedRoom:
                    HandleSuccess(message);
                    break;
            }
        }

        private async void HandleRoomCreation(MessageEnvelope message)
        {
            var roomName = message.KeyValuePairs["RoomName"];
            var password = message.KeyValuePairs["Pass"];

            // room names are unique, end of story!

            //await connection.CreateOrJoinRoom(roomName, password);
        }

        private void HandleSuccess(MessageEnvelope message)
        {
            throw new NotImplementedException();
        }
    }
}
