using MessageProtocol;
using System;
using System.Collections.Generic;
using System.Text;
using NetworkLibrary.MessageProtocol;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Net.Sockets;
using NetworkLibrary.TCP.AES;
using NetworkLibrary.Components.Crypto;
using NetworkLibrary.Components;
using NetworkLibrary.TCP.ByteMessage;
using NetworkLibrary.Components.Crypto.DiffieHellman;
using NetworkLibrary.Components.Crypto.KeyDerivation;
using NetworkLibrary.DistributedP2P.Components;
using NetworkLibrary.DistributedP2P.Client.StateManagement;
using NetworkLibrary.TCP.SSL.Base;

namespace NetworkLibrary.DistributedP2P.Client
{
    public class DistributedLobbyClient<S>:IClientConnection where S : ISerializer, new()
    {
        IClientDbConnection clientDbConnector;
        IClientAuthenticationProvider clientAuthProvider;
        SecureMessageClient<S> sslClient;
        StateManager stateManager =  new StateManager();
        public DistributedLobbyClient(IClientDbConnection clientDbConnector, X509Certificate2 certificate = null)
        {
            this.clientDbConnector = clientDbConnector;
            sslClient = new SecureMessageClient<S>(certificate);
            sslClient.OnMessageReceived += HandleServerMsg;
        }

       

        public async Task<bool> ConnectAsync(string ip, int port)
        {
            IClientAuthenticationToken authToken = clientAuthProvider.Authenticate();

            bool res = await sslClient.ConnectAsync(ip, port);
            if (res)
            {
                Guid conversationId = Guid.NewGuid();
                var conState = new ClientConnectionState(conversationId, this, clientDbConnector, authToken);
                stateManager.RegisterState(conState);

                await conState.WaitCompletion();

                if (conState.IsSuccesful)
                {
                    return true;
                }

                return false;

            }
            else return false;
        }

        public void SenAsyncMessage(MessageEnvelope message)
        {
            sslClient.SendAsyncMessage(message);
        }

        public async Task<MessageEnvelope> SendMessageAndWaitResponse(MessageEnvelope message)
        {
            return await sslClient.SendMessageAndWaitResponse(message);
        }

        public async Task<ITcpChannel> OpenTcpChannel(Guid destinationPeer, ChannelInfo Info)
        {
           var pipeState = new ClientPipeState(Guid.NewGuid(), this);
           stateManager.RegisterState(pipeState);
           pipeState.Start(destinationPeer);

            await pipeState.WaitCompletion();

            if (pipeState.IsSuccesful)
            {
                var symetricKey = await PerformDHWithPeer(destinationPeer);
                if (symetricKey != null)
                {
                    var channel = new SecureTcpChannel(Info, pipeState.ConnectedSocket, symetricKey);
                    return channel;
                }
            }
            return null;
        }

        private async Task<byte[]> PerformDHWithPeer(Guid destinationPeer)
        {
            // lets do this first
            // so how the other party associates this with the channel.
            // maybe we need channel creation state machine.

            DiffieHellman df = new DiffieHellman();
            byte[] publicKey = df.GetPublicKey();

            MessageEnvelope envelope = new MessageEnvelope();
            envelope.Header = "DH";
            envelope.To = destinationPeer;
            envelope.Payload = publicKey;


            var response = await SendMessageAndWaitResponse(envelope);
            if (response.Header != MessageEnvelope.RequestTimeout)
            {
                response.LockBytes();
                byte[] dstPublic = response.Payload;

                var secret = df.CalculateSharedSecret(dstPublic);
                var symetricKey = HKDFLite.DeriveKey(secret, outputLength: 16);
                return symetricKey;
            }
            return null;
        }



        private void HandleServerMsg(MessageEnvelope envelope)
        {
            if (envelope.IsInternal)
            {
                if (stateManager.HandleMessage(envelope))
                    return;

                switch (envelope.Header) 
                {
                    case InternalConstants.PipeTokenDelivery:

                        var pipeState = new ClientPipeState(envelope.MessageId, this);
                        pipeState.OnComplete += HandlePipeCreated;
                        stateManager.RegisterState(pipeState);
                        pipeState.HandleMessage(envelope);
                        break;
                
                }
            }
        }

        private void HandlePipeCreated(IConversationState state)
        {
           if(state.IsSuccesful)
           {
               // notify that a connection is opened, like socket accept
           }
        }

        public void Disconnect()
        {
            sslClient.Disconnect();
        }


    }
}
