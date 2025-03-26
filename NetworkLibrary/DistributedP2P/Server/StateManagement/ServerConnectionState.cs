using NetworkLibrary.DistributedP2P.Components;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkLibrary.DistributedP2P.Server.StateManagement
{
    internal class ServerConnectionState : ConversationStateBase
    {
        public IClientDbInfo clientDbInfo { get; private set; }

        public readonly Guid EphemeralClientId;

        private readonly IDistributedConnection connection;
        private readonly IAuthenticator authenticator;
        private readonly IServerDbConnector dbConnector;
        private IAuthenticationResult tokenResult;

        public ServerConnectionState(Guid stateId, Guid clientId, IDistributedConnection connection, IAuthenticator authenticator, IServerDbConnector dbConnector):base(stateId)
        {
            this.EphemeralClientId = clientId;
            this.connection = connection;
            this.authenticator = authenticator;
            this.dbConnector = dbConnector;
        }

        internal void Start()
        {
            var msg = CreateEnvelope();
            msg.Header = InternalConstants.ConnectionStart;
            connection.SendAsyncMessage(EphemeralClientId, msg);
        }

        public override void HandleMessage(MessageEnvelope message)
        {
            if (IsCompleted())
                return;
            switch (message.Header)
            {
                case InternalConstants.ConnectionReq:
                    HandleInitialConnectionRequest(message);
                    break;

                case InternalConstants.ConnectionAckClientPublicData:
                    HandleClientPublicData(message);
                    break;
            }
        }



        private void HandleInitialConnectionRequest(MessageEnvelope message)
        {
            message.KeyValuePairs.TryGetValue("AuthToken", out string token);
            message.KeyValuePairs.TryGetValue("AuthMethod", out string method);
            message.KeyValuePairs.TryGetValue("AdditionalData", out string additionalData);

            if (token == null)
            {
                ReplyError("No token provided");
                return;
            }

            if (method == null)
            {
                ReplyError("No authentication method provided");
                return;
            }

            authenticator.Authenticate(token, method, additionalData).ContinueWith(HandleAuthentication);
        }

        private void HandleAuthentication(Task<IAuthenticationResult> task)
        {
            if (IsCompleted())
                return;

            IAuthenticationResult result = task.Result;

            if (result.IsValid)
            {
                FindClientDBLink(result);
            }
            else
            {
                ReplyError(result.Error);
            }
        }

        private void FindClientDBLink(IAuthenticationResult tokenResult)
        {
            dbConnector.GetClientData(tokenResult).ContinueWith(t => HandleClientData(t.Result, tokenResult));
        }

        private void HandleClientData(IClientDbInfo dbResult, IAuthenticationResult tokenResult)
        {
            if (IsCompleted())
                return;

            if (dbResult.IsValid)
            {
                clientDbInfo = dbResult;
                ReplyGood();
            }
            else
            {
                this.tokenResult = tokenResult;
                RegisterClient();
            }
        }

        private void RegisterClient()
        {
            var msg = CreateEnvelope();
            msg.Header = InternalConstants.ConnectionGetClientPublicData;
            connection.SendAsyncMessage(EphemeralClientId, msg);
        }

        private void HandleClientPublicData(MessageEnvelope message)
        {
            message.LockBytes();
            dbConnector.RegisterClient(tokenResult, message.Payload).ContinueWith(HandleDbRegistration);
        }

        private void HandleDbRegistration(Task<IClientDbInfo> task)
        {
            if (IsCompleted())
                return;

            IClientDbInfo dbResult = task.Result;
            if (dbResult.IsValid)
            {
                clientDbInfo = dbResult;
                ReplyGood();
            }
            else
            {
                ReplyError(dbResult.Error);
            }
        }

        private void ReplyGood()
        {
            var msg = CreateEnvelope();
            msg.Header = InternalConstants.ConnectionAckGood;

            lock (cancellationMutex)
            {
                if (IsCompleted())
                    return;

                connection.SendAsyncMessage(EphemeralClientId, msg);
                Completed(true);
            }

        }

        private void ReplyError(string err)
        {
            connection.SendAsyncMessage(EphemeralClientId, CreateErrorMsg(err));
            Completed(false);
        }

      

    }
}
