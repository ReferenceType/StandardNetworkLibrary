using NetworkLibrary.Components.Statistics;
using NetworkLibrary.TCP.ByteMessage;
using NetworkLibrary.TCP.SSL.ByteMessage;
using NetworkLibrary.Utils;
using ProtoBuf;
using Protobuff;
using Protobuff.P2P;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Examples
{
    internal class Program
    {
        static void Main(string[] args)
        {
             ExampleByteMessage();
            //ExampleProtoSecure();
            //ExampleProtobuff();
            //ExampleSecureByteMessage();#
           // ExampleSecureP2P();
        }

      

        private static void ExampleByteMessage()
        {
            ByteMessageTcpServer server = new ByteMessageTcpServer(20008);
            server.OnBytesReceived += ServerBytesReceived;
            server.StartServer();

            ByteMessageTcpClient client = new ByteMessageTcpClient();
            client.OnBytesReceived += ClientBytesReceived;
            client.Connect("127.0.0.1", 20008);

            client.SendAsync(UTF8Encoding.UTF8.GetBytes("Hello I'm a client!"));

            void ServerBytesReceived(in Guid clientId, byte[] bytes, int offset, int count)
            {
                Console.WriteLine(UTF8Encoding.UTF8.GetString(bytes, offset, count));
                server.SendBytesToClient(clientId, UTF8Encoding.UTF8.GetBytes("Hello I'm the server"));
            }

            void ClientBytesReceived(byte[] bytes, int offset, int count)
            {
                Console.WriteLine(UTF8Encoding.UTF8.GetString(bytes, offset, count));
            }
        }

        private static void ExampleSecureByteMessage()
        {
            var scert = new X509Certificate2("server.pfx", "greenpass");
            var cert = new X509Certificate2("client.pfx", "greenpass");

            var server = new SslByteMessageServer(20008,scert);
            server.OnBytesReceived += ServerBytesReceived;
            // since certificate is self-signed it will give chain errors, here we bypass it
            server.RemoteCertificateValidationCallback += (a, b, c, d) => true;
            server.StartServer();
           

            var client = new SslByteMessageClient(cert);
            client.OnBytesReceived += ClientBytesReceived;
            client.RemoteCertificateValidationCallback += (a, b, c, d) => true;
            client.Connect("127.0.0.1", 20008);

            client.SendAsync(UTF8Encoding.ASCII.GetBytes("Hello I'm a client!"));

            void ServerBytesReceived(in Guid clientId, byte[] bytes, int offset, int count)
            {
                Console.WriteLine(UTF8Encoding.ASCII.GetString(bytes, offset, count));
                server.SendBytesToClient(clientId, UTF8Encoding.ASCII.GetBytes("Hello I'm the server"));
            }

            void ClientBytesReceived(byte[] bytes, int offset, int count)
            {
                Console.WriteLine(UTF8Encoding.ASCII.GetString(bytes, offset, count));
            }
        }


        [ProtoContract]
        class SamplePayload : IProtoMessage
        {
            [ProtoMember(1)]
            public string sample;
        }


        private static async Task ExampleProtoSecure()
        {
            var scert = new X509Certificate2("server.pfx", "greenpass");
            var cert = new X509Certificate2("client.pfx", "greenpass");

            SecureProtoServer server = new SecureProtoServer(20008, scert);
            server.OnMessageReceived += ServerMessageReceived;

            var client = new SecureProtoClient(cert);
            client.OnMessageReceived += ClientMessageReceived;
            client.Connect("127.0.0.1", 20008);

            var Payload = new SamplePayload() { sample = "Hello" };
            var messageEnvelope = new MessageEnvelope();

            // You can just send a message, get replies on ClientMessageReceived.
            client.SendAsyncMessage(messageEnvelope);
            client.SendAsyncMessage(messageEnvelope, Payload);

            // Or you can wait for a reply async.
            MessageEnvelope result = await client.SendMessageAndWaitResponse(messageEnvelope, Payload);
            var payload = result.UnpackPayload<SamplePayload>();

            void ServerMessageReceived(in Guid clientId, MessageEnvelope message)
            {
                server.SendAsyncMessage(in clientId, message);
            }

            void ClientMessageReceived(MessageEnvelope message)
            {
            }
        }

       
        private static async Task ExampleProtobuff()
        {
            var server = new ProtoServer(20008);
            server.OnMessageReceived += ServerMessageReceived;

            var client = new ProtoClient();
            client.OnMessageReceived += ClientMessageReceived;
            client.Connect("127.0.0.1", 20008);

            var Payload = new SamplePayload() { sample = "Hello" };
            var messageEnvelope = new MessageEnvelope();

            // You can just send a message, get replies on ClientMessageReceived.
            client.SendAsyncMessage(messageEnvelope);
            client.SendAsyncMessage(messageEnvelope, Payload);

            // Or you can wait for a reply async.
            MessageEnvelope result = await client.SendMessageAndWaitResponse(messageEnvelope, Payload);
            var payload = result.UnpackPayload<SamplePayload>();

            void ServerMessageReceived(in Guid clientId, MessageEnvelope message)
            {
                server.SendAsyncMessage(in clientId, message);
            }

            void ClientMessageReceived(MessageEnvelope message)
            {
            }
        }

       
        private static void ExampleSecureP2P()
        {
            var ipEndpoint = new IPEndPoint(12, 1);
            var cert = new X509Certificate2("client.pfx", "greenpass");
            var scert = new X509Certificate2("server.pfx", "greenpass");

            var server = new SecureProtoRelayServer(20010, scert);

            var clients = new List<RelayClient>();
            for (int i = 0; i < 2; i++)
            {
                var client = new RelayClient(cert);
                client.OnMessageReceived += (reply) => ClientMsgReceived(client, reply);
                client.OnUdpMessageReceived += (reply) => ClientUdpReceived(client, reply);
                client.OnPeerRegistered += (peerId) => OnPeerRegistered(client, peerId);

                client.Connect("127.0.0.1", 20010);
                clients.Add(client);
            }
            Thread.Sleep(3000);

            var destinationId = clients[0].Peers.First();
            var succes = clients[0].RequestHolePunchAsync(destinationId, 5000).Result;

            var message = new SamplePayload() { sample = "hello" };
            clients[0].SendAsyncMessage(destinationId,message);
           
            // message handling
            void OnPeerRegistered(RelayClient client, Guid peerId)
            {
                Console.WriteLine(client.sessionId + " Says: A peer registered: "+peerId);
            }
            void ClientMsgReceived(RelayClient client, MessageEnvelope reply)
            {
                Console.WriteLine(client.sessionId + " Says: messageReceived from: " + reply.From);
                Console.WriteLine(client.sessionId + " Says: sending a udp message to: " + reply.From);
                client.SendUdpMesssage(reply.From, reply);
            }

            void ClientUdpReceived(RelayClient client, MessageEnvelope reply)
            {
                Console.WriteLine(client.sessionId + " Says: Udp messageReceived from: " + reply.From);

            }
        }
    }
}