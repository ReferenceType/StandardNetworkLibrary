using NetworkLibrary;
using NetworkLibrary.TCP.ByteMessage;
using NetworkLibrary.TCP.SSL.ByteMessage;
using NetworkLibrary.Utils;
using ProtoBuf;
using Protobuff;
using Protobuff.P2P;
using Protobuff.Pure;
using System.Net.Http.Headers;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Examples
{
    internal class Program
    {
        static void Main(string[] args)
        {
            MiniLogger.AllLog += (log)=>Console.WriteLine(log);
            //ExampleByteMessage();
            //ExampleSecureByteMessage();

            //ExamplePureProto();
            //ExamplePureSecureProto();

            //ExampleProtoMessageProtocol();
            ExampleProtoMessageProtocolSecure();

            //ExampleSecureP2P();
            //ExampleRoomServer();
            var msg = new MessageEnvelope();
            
            Console.ReadLine();
        }
        #region Byte Message
        private static void ExampleByteMessage()
        {
            ByteMessageTcpServer server = new ByteMessageTcpServer(20008);
            server.OnBytesReceived += ServerBytesReceived;
            server.StartServer();

            ByteMessageTcpClient client = new ByteMessageTcpClient();
            client.OnBytesReceived += ClientBytesReceived;
            client.Connect("127.0.0.1", 20008);

            client.SendAsync(Encoding.UTF8.GetBytes("Hello I'm a client!"));

            void ServerBytesReceived(Guid clientId, byte[] bytes, int offset, int count)
            {
                Console.WriteLine(Encoding.UTF8.GetString(bytes, offset, count));
                server.SendBytesToClient(clientId, Encoding.UTF8.GetBytes("Hello I'm the server"));
            }

            void ClientBytesReceived(byte[] bytes, int offset, int count)
            {
                Console.WriteLine(Encoding.UTF8.GetString(bytes, offset, count));
            }
        }

        private static void ExampleSecureByteMessage()
        {
            var scert = new X509Certificate2("server.pfx", "greenpass");
            var cert = new X509Certificate2("client.pfx", "greenpass");

            var server = new SslByteMessageServer(20008, scert);
            server.RemoteCertificateValidationCallback += (_, _, _, _) => { return true; };

            server.OnBytesReceived += ServerBytesReceived;
            // since certificate is self-signed it will give chain errors, here we bypass it
            server.RemoteCertificateValidationCallback += (a, b, c, d) => true;
            server.StartServer();


            var client = new SslByteMessageClient(cert);
            client.OnBytesReceived += ClientBytesReceived;
            client.RemoteCertificateValidationCallback += (a, b, c, d) => true;
            client.Connect("127.0.0.1", 20008);

            client.SendAsync(UTF8Encoding.ASCII.GetBytes("Hello I'm a client!"));

            void ServerBytesReceived(Guid clientId, byte[] bytes, int offset, int count)
            {
                Console.WriteLine(UTF8Encoding.ASCII.GetString(bytes, offset, count));
                server.SendBytesToClient(clientId, UTF8Encoding.ASCII.GetBytes("Hello I'm the server"));
            }

            void ClientBytesReceived(byte[] bytes, int offset, int count)
            {
                Console.WriteLine(UTF8Encoding.ASCII.GetString(bytes, offset, count));
            }
        }
        #endregion

        #region Pure Seialized Server/Client

        [ProtoContract]
        class SampleMessage : IProtoMessage
        {
            [ProtoMember(1)]
            public string sample;
        }
        private static void ExamplePureProto()
        {
            PureProtoServer server = new PureProtoServer(11234);
            server.StartServer();

            server.BytesReceived += (clientId,bytes, offset, count) => 
            {
                SampleMessage msg = server.Serializer.Deserialize<SampleMessage>(bytes, offset, count);
                Console.WriteLine(msg.sample);
                msg.sample = "Jesse Lets cook";
                server.SendAsync(clientId,msg);
            };

            PureProtoClient client = new PureProtoClient();
            client.Connect("127.0.0.1", 11234);

            client.BytesReceived += (bytes, offset, count) =>
            {
                SampleMessage msg = client.Serializer.Deserialize<SampleMessage>(bytes, offset, count);
                Console.WriteLine(msg.sample);
            };

            client.SendAsync(new SampleMessage() { sample = "Yo! Mr White" });
            Console.ReadLine();
        }

        private static void ExamplePureSecureProto()
        {
            var scert = new X509Certificate2("server.pfx", "greenpass");
            var ccert = new X509Certificate2("client.pfx", "greenpass");

            var server = new PureSecureProtoServer(11111,scert);
            server.RemoteCertificateValidationCallback += (_, _, _, _) => { return true; };
            server.StartServer();

            server.BytesReceived += (clientId, bytes, offset, count) =>
            {
                SampleMessage msg = server.Serializer.Deserialize<SampleMessage>(bytes, offset, count);
                Console.WriteLine(msg.sample);
                msg.sample = "Jesse Lets cook";
                server.SendAsync(clientId, msg);
            };

            var client = new PureSecureProtoClient(ccert);
            client.RemoteCertificateValidationCallback += (_, _, _, _) => { return true; };
            client.Connect("127.0.0.1", 11111);

            client.BytesReceived += (bytes, offset, count) =>
            {
                SampleMessage msg = client.Serializer.Deserialize<SampleMessage>(bytes, offset, count);
                Console.WriteLine(msg.sample);
            };
            client.SendAsync(new SampleMessage() { sample = "Yo! Mr White" });
            Console.ReadLine();
        }
        #endregion

        #region Message Protocol
        [ProtoContract]
        class SamplePayload : IProtoMessage
        {
            [ProtoMember(1)]
            public string sample;
        }


        private static async Task ExampleProtoMessageProtocolSecure()
        {
            var scert = new X509Certificate2("server.pfx", "greenpass");
            var cert = new X509Certificate2("client.pfx", "greenpass");

            SecureProtoMessageServer server = new SecureProtoMessageServer(20008, scert);
            server.StartServer();
            server.RemoteCertificateValidationCallback += (_, _, _, _) => { return true; };
            server.OnMessageReceived += ServerMessageReceived;

            var client = new SecureProtoMessageClient(cert);
            client.OnMessageReceived += ClientMessageReceived;
            client.Connect("127.0.0.1", 20008);

            var Payload = new SamplePayload() { sample = "Hello" };
            var messageEnvelope = new MessageEnvelope();
            messageEnvelope.Header = "PayloadTest";

            // You can just send a message, get replies on ClientMessageReceived.
            client.SendAsyncMessage(messageEnvelope);
            client.SendAsyncMessage(messageEnvelope, Payload);

            // Or you can wait for a reply async.
            MessageEnvelope result = await client.SendMessageAndWaitResponse(messageEnvelope, Payload);
            var payload = result.UnpackPayload<SamplePayload>();
            Console.WriteLine($"Client Got Response {payload.sample}");

            void ServerMessageReceived(Guid clientId, MessageEnvelope message)
            {
                Console.WriteLine($"Server Received message {message.Header}");
                server.SendAsyncMessage(clientId, message);
            }

            void ClientMessageReceived(MessageEnvelope message)
            {
            }
        }

        private static async Task ExampleProtoMessageProtocol()
        {
            var server = new ProtoMessageServer(20008);
            server.OnMessageReceived += ServerMessageReceived;

            var client = new ProtoMessageClient();
            client.OnMessageReceived += ClientMessageReceived;
            client.Connect("127.0.0.1", 20008);

            var Payload = new SamplePayload() { sample = "Hello" };
            var messageEnvelope = new MessageEnvelope();
            messageEnvelope.Header = "PayloadTest";

            // You can just send a message, get replies on ClientMessageReceived.
            client.SendAsyncMessage(messageEnvelope);
            client.SendAsyncMessage(messageEnvelope, Payload);

            // Or you can wait for a reply async.
            MessageEnvelope result = await client.SendMessageAndWaitResponse(messageEnvelope, Payload);
            var payload = result.UnpackPayload<SamplePayload>();
            Console.WriteLine($"Client Got Response {payload.sample}");

            void ServerMessageReceived(Guid clientId, MessageEnvelope message)
            {
                Console.WriteLine($"Server Received message {message.Header}");
                server.SendAsyncMessage(clientId, message);
            }

            void ClientMessageReceived(MessageEnvelope message)
            {
            }
        }
        #endregion

        #region P2P
        private static void ExampleSecureP2P()
        {
            var cert = new X509Certificate2("client.pfx", "greenpass");
            var scert = new X509Certificate2("server.pfx", "greenpass");
            
            var server = new SecureProtoRelayServer(20010, scert);
            server.RemoteCertificateValidationCallback += (_, _, _, _) => { return true; };
            server.StartServer();

            var clients = new List<RelayClient>();
            for (int i = 0; i < 2; i++)
            {
                var client = new RelayClient(cert);
                client.OnMessageReceived += (reply) => ClientMsgReceived(client, reply);
                client.OnUdpMessageReceived += (reply) => ClientUdpReceived(client, reply);
                client.OnPeerRegistered += (peerId) => OnPeerRegistered(client, peerId);

                client.RemoteCertificateValidationCallback += (_, _, _, _) => { return true; };

                client.Connect("127.0.0.1", 20010);
                clients.Add(client);
            }
            Thread.Sleep(3000);

            Guid destinationId = clients[0].Peers.First().Key;
            var succes = clients[0].RequestHolePunchAsync(destinationId, 5000).Result;

            var message = new SamplePayload() { sample = "hello" };
            clients[0].SendAsyncMessage(destinationId, message);

            // message handling
            void OnPeerRegistered(RelayClient client, Guid peerId)
            {
                Console.WriteLine(client.SessionId + " Says: A peer registered: " + peerId);
            }
            void ClientMsgReceived(RelayClient client, MessageEnvelope reply)
            {
                Console.WriteLine(client.SessionId + " Received Message From " + reply.From);
                Console.WriteLine(client.SessionId + " Sending Udp message to: " + reply.From);
                client.SendUdpMesssage(reply.From, reply);
            }

            void ClientUdpReceived(RelayClient client, MessageEnvelope reply)
            {
                Console.WriteLine(client.SessionId + "Udp Message Received from: " + reply.From);

            }
        }
        private static void ExampleRoomServer()
        {
            var cert = new X509Certificate2("client.pfx", "greenpass");
            var scert = new X509Certificate2("server.pfx", "greenpass");

            var server = new SecureProtoRoomServer(20010, scert);
            server.RemoteCertificateValidationCallback += (_, _, _, _) => { return true; };
            server.StartServer();

            var client1 = new SecureProtoRoomClient(cert);
            client1.OnTcpRoomMesssageReceived += (roomName, m) => Console.WriteLine($"Tcp Received from Room [{roomName}] -> " + m.Header);
            client1.OnUdpRoomMesssageReceived += (roomName, m) => Console.WriteLine($"Udp Received from Room [{roomName}] -> " + m.Header);
            client1.OnTcpMessageReceived += (m) => Console.WriteLine("Private Tcp Received -> " + m.Header);
            client1.OnUdpMessageReceived += (m) => Console.WriteLine("Private Udp Received ->" + m.Header);

            client1.RemoteCertificateValidationCallback += (_, _, _, _) => { return true; };
            client1.Connect("127.0.0.1", 20010);
            client1.CreateOrJoinRoom("MyGarage");
           
            var client2 = new SecureProtoRoomClient(cert);
            client2.OnTcpRoomMesssageReceived += (roomName, m) => Console.WriteLine($"Tcp Received from Room [{roomName}] -> " + m.Header);
            client2.OnUdpRoomMesssageReceived += (roomName, m) => Console.WriteLine($"Udp Received from Room [{roomName}] -> " + m.Header);
            client2.OnTcpMessageReceived += (m) => Console.WriteLine("Private Tcp Received -> " + m.Header);
            client2.OnUdpMessageReceived += (m) => Console.WriteLine("Private Udp Received ->" + m.Header);

            client2.Connect("127.0.0.1", 20010);
            client2.CreateOrJoinRoom("MyGarage");
            Thread.Sleep(1000);

            Guid peerId = client2.SessionId;
            var succes = client1.RequestHolePunchAsync(peerId, 5000).Result;
            Thread.Sleep(1000);
            Console.WriteLine("");

            client1.SendUdpMessageToRoom("MyGarage", new MessageEnvelope() { Header = "Hello Udp", Payload = new byte[128000] });
            client1.SendRudpMessageToRoom("MyGarage", new MessageEnvelope() { Header = "Hello Rudp", Payload = new byte[128000] });
            client1.SendMessageToRoom("MyGarage", new MessageEnvelope() { Header = "Hello Tcp", Payload = new byte[128000] });

            client1.SendUdpMessageToPeer(peerId, new MessageEnvelope() { Header = "Hello Private Udp", Payload = new byte[128000] });
            client1.SendRudpMessageToPeer(peerId, new MessageEnvelope() { Header = "Hello Private Rudp", Payload = new byte[128000] });
            client1.SendMessageToPeer(peerId, new MessageEnvelope() { Header = "Hello Private Tcp", Payload = new byte[128000] });
        }
        #endregion
    }
}