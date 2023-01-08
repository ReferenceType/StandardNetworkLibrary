# .Net Standard Network Library.
High Performance Network library for Tcp Udp network applications.
Supported Frameworks .Net Standard 2.0+.
## Features
- Tcp and SSL high performance server & clients. Supports native byte message protocol with 4 byte int size header.
- Udp server client model with AES encyrption support.
- Protobuf message server client models. Based on raw and encrypted Tcp And Udp servers on core libraries.
- P2P topology support using protobuf messages by Relay server & client model.
- Udp holepunching on P2P network

### Internal Architectural Features
- Global shared memory pool where each byte array is rented and returned. Each memory user including memory stream backing buffers are rented though the pool.
Pool`s memory is internally maintained for trimming. Protobuf serialisation also shares this pool. Memory footprint is low and scaleable and low GC pressure.

- System calls for socket send and recieve are expensive. When Tcp based servers are on high load, messages are stiched together to increase throughput, idea is to send multiple messages in one system call. This is only for high load so there is no latency issue on regular traffic. This gather sysytem is configrable and can be based on queue or stream swaps.



# Documentation
TODO one day when i have the time.. 


# Sample Code 
## Base Byte Message TCP Server Client
```c#
        private static void ExampleByteMessage()
        {
            ByteMessageTcpServer server = new ByteMessageTcpServer(20008);
            server.OnBytesReceived += ServerBytesReceived;
            server.StartServer();

            ByteMessageTcpClient client = new ByteMessageTcpClient();
            client.OnBytesReceived += ClientBytesReceived;
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
 ```
```Console
output:
Hello I'm a client!
Hello I'm the server
```
## Secure Proto Client Server
Declare your type
```c#
        [ProtoContract]
        class SamplePayload :IProtoMessage
        {
            [ProtoMember(1)]
            public string sample;
        }
```
``` c#
  private static async Task ExampleProtoSecure()
        {
            var scert = new X509Certificate2("server.pfx", "greenpass");
            var cert = new X509Certificate2("client.pfx", "greenpass");

            SecureProtoServer server = new SecureProtoServer(20008, 100, scert);
            server.OnMessageReceived += ServerMessageReceived;

            var client = new SecureProtoClient(cert);
            client.OnMessageReceived += ClientMessageReceived;
            client.Connect("127.0.0.1", 20008);

            var Payload = new SamplePayload() { sample = "Hello" };
            var messageEnvelope = new MessageEnvelope();

            // You can just send a message, get replies on ClientMessageReceived.
            client.SendAsyncMessage(messageEnvelope);
            client.SendAsyncMessage(messageEnvelope,Payload);

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
```
# Benchmarks

Infinite Echo benchmarks are done by sending set of messages to server and getting echo reply. I will publish more detailed anlysis in future the story isnt so simple.
Tests are done on my personal laptop with AMD Ryzen 7 5800H.

Preliminary benchmarks are as follows:
### Tcp Byte Message Server: 100 clients 1000 seed messages (32 bytes + 4 header) each:

~65,000,000 echo per second.

### SSL Byte message: 100 clients 1000 seed messages(32 bytes + 4 header) each:

~ 44,600,000 echo per second.

### Secure Protobuf Client Server 100 clients 1000 seed messages ( 32 byte payload 48 byte total):

~ 3,500,000 request - response per second.
