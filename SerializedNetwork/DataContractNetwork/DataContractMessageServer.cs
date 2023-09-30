using DataContractNetwork.Components;
using NetworkLibrary.Components.Statistics;
using NetworkLibrary.MessageProtocol;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static DataContractNetwork.Internal.ServerClientSession;

namespace DataContractNetwork
{
    public class DataContractMessageServer
    {
        public delegate void MessageReceived(Guid clientId, MessageEnvelope message);
        public MessageReceived OnMessageReceived;

        public Action<Guid> OnClientAccepted;
        public Action<Guid> OnClientDisconnected;

        internal readonly DataContractServerIntenal server;
        private GenericMessageSerializer<MessageEnvelope, DataContractSerialiser> serialiser
            = new GenericMessageSerializer<MessageEnvelope, DataContractSerialiser>();


        public DataContractMessageServer(int port)
        {
            //awaiter = new MessageAwaiter();

            server = new DataContractServerIntenal(port);
            server.OnClientAccepted += HandleClientAccepted;
            server.OnMessageReceived += HandleMessageReceived;
            server.OnClientDisconnected += HandleClientDisconnected;

            server.MaxIndexedMemoryPerClient = 128000000;
            server.StartServer();

        }

        private void HandleMessageReceived(Guid id, MessageEnvelope message)
        {
            OnMessageReceived?.Invoke(id, message);
        }

      
        protected bool CheckAwaiter(MessageEnvelope message)
        {
            if (server.Awaiter.IsWaiting(message.MessageId))
            {
                message.LockBytes();
                server.Awaiter.ResponseArrived(message);
                return true;
            }
            return false;
        }

        public void GetStatistics(out TcpStatistics generalStats, out ConcurrentDictionary<Guid, TcpStatistics> sessionStats)
           => server.GetStatistics(out generalStats, out sessionStats);

        public IPEndPoint GetIPEndPoint(Guid cliendId)
            => server.GetSessionEndpoint(cliendId);

        #region Send
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(Guid clientId, MessageEnvelope message)
        {
            server.SendAsyncMessage(clientId, message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage(Guid clientId, MessageEnvelope message, byte[] buffer, int offset, int count)
        {
            server.SendAsyncMessage(clientId, message, buffer, offset, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SendAsyncMessage<T>(Guid clientId, MessageEnvelope message, T payload)
        {
            server.SendAsyncMessage(clientId, message, payload);
        }
        #endregion

        #region SendAndWait
        public Task<MessageEnvelope> SendMessageAndWaitResponse<T>(Guid clientId, MessageEnvelope message, byte[] buffer, int offset, int count, int timeoutMs = 10000)
        {
            return server.SendMessageAndWaitResponse<MessageEnvelope>(clientId, message, buffer, offset, count);
        }

        public Task<MessageEnvelope> SendMessageAndWaitResponse<T>(Guid clientId, MessageEnvelope message, T payload, int timeoutMs = 10000)
        {
            return server.SendMessageAndWaitResponse(clientId, message, payload, timeoutMs);
        }

        public Task<MessageEnvelope> SendMessageAndWaitResponse(Guid clientId, MessageEnvelope message, int timeoutMs = 10000)
        {
            return server.SendMessageAndWaitResponse(clientId, message, timeoutMs);
        }
        #endregion

        protected virtual void HandleClientAccepted(Guid clientId)
            => OnClientAccepted?.Invoke(clientId);
        protected virtual void HandleClientDisconnected(Guid guid)
            => OnClientDisconnected?.Invoke(guid);
        public void Shutdown()
            => server.ShutdownServer();
    }
}
