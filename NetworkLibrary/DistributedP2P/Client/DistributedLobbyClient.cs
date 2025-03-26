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
            // so here somehow we will get a socet.
            // its either through holepunch or through the server

            Socket socket = await OpenTcpPipeWithPeer(destinationPeer);

            // Socket is either a server or a client.


            if (socket != null)
            {
                if (Info.AesMode != AesMode.None)
                {
                    // we obtain here shared Key with peer;
                    byte[] sharedSecret = await PerformDHWithPeer(destinationPeer);


                    var alg = new ConcurrentAesAlgorithm(sharedSecret, Info.AesMode);
                    return new SecureTcpChannel(new AesTcpClient(socket, alg), Info);

                }
                else
                {
                    return new SecureTcpChannel(new AesTcpClient(socket, new ConcurrentAesAlgorithm(new byte[16], Info.AesMode)), Info);
                }
                    
            }
            else
            {
                return null;
            }
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

        private Task<Socket> OpenTcpPipeWithPeer(Guid destinationPeer)
        {
            // Shenanigans with server
            // First try holepunch
            // Then open a pipe with the server

            return null;
        }

        public void Disconnect()
        {
            sslClient.Disconnect();
        }


    }
}
