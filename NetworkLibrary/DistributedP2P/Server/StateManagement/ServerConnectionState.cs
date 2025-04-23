using NetworkLibrary.DistributedP2P.Components;
using System;
using System.Collections.Generic;
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
        private readonly int endpointDiscoveryServerPort;
        private IAuthenticationResult tokenResult;

        public List<string> clientLocalIps;

        int timeSynced;
        int handShakeComplete;

        public ServerConnectionState(Guid stateId, Guid clientId, IDistributedConnection connection, IAuthenticator authenticator, IServerDbConnector dbConnector, int EDSPort, ILogger logger) : base(stateId, 20000, logger)
        {
            this.EphemeralClientId = clientId;
            this.connection = connection;
            this.authenticator = authenticator;
            this.dbConnector = dbConnector;
            endpointDiscoveryServerPort = EDSPort;
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

                case InternalConstants.SyncTime:
                    HandleTimeSyncComplete(message);
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
            message.KeyValuePairs.Remove("AuthToken");
            message.KeyValuePairs.Remove("AuthMethod");
            if (message.KeyValuePairs.ContainsKey("AdditionalData"))
                message.KeyValuePairs.Remove("AdditionalData");

            clientLocalIps = new List<string>();

            foreach (var kv in message.KeyValuePairs)
            {
                clientLocalIps.Add(kv.Key);
            }

            authenticator.Authenticate(token, method, additionalData).ContinueWith(HandleAuthentication, TaskScheduler.Default);
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
            dbConnector.GetClientData(tokenResult).ContinueWith(t => HandleClientData(t.Result, tokenResult), TaskScheduler.Default);
        }

        private void HandleClientData(IClientDbInfo dbResult, IAuthenticationResult tokenResult)
        {
            if (IsCompleted())
                return;

            if (dbResult.IsValid)
            {
                clientDbInfo = dbResult;
                HandShakecomplete();
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
            dbConnector.RegisterClient(tokenResult, message.Payload).ContinueWith(HandleDbRegistration, TaskScheduler.Default);
        }

        private void HandleDbRegistration(Task<IClientDbInfo> task)
        {
            if (IsCompleted())
                return;

            IClientDbInfo dbResult = task.Result;
            if (dbResult.IsValid)
            {
                clientDbInfo = dbResult;
                HandShakecomplete();
            }
            else
            {
                ReplyError(dbResult.Error);
            }
        }

        private void HandleTimeSyncComplete(MessageEnvelope message)
        {
            if (Interlocked.Exchange(ref timeSynced, 1) == 0)
            {
                if (Interlocked.CompareExchange(ref handShakeComplete, 0, 0) == 1)
                    SendGood();
            }

        }

        private void HandShakecomplete()
        {
            if (Interlocked.Exchange(ref handShakeComplete, 1) == 0)
            {
                if (Interlocked.CompareExchange(ref timeSynced, 0, 0) == 1)
                    SendGood();
            }
        }

        private void SendGood()
        {
            var msg = CreateEnvelope();
            msg.Header = InternalConstants.ConnectionAckGood;
            msg.To = EphemeralClientId;
            msg.KeyValuePairs = new Dictionary<string, string>();
            msg.KeyValuePairs["EDPort"] = endpointDiscoveryServerPort.ToString();
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
