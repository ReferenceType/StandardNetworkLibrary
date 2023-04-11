# .Net Standard Network Library.
High Performance easy to use Network library supports 16k+ clients. 
This library Consist of two assemblies.
 - Core Network Library, which is the is the core high performance library and works only with bytes. It offers extensibility with direct serialisation on any protocol. All features are designed from scracth. Assembly does not require any external package, you can use the dll directly. It also offers great utilites such as pooled memory streams. It supports up to 16k clients then, my system(Windows Home) runs out of ports.

 - Protobuff Assembly where I used the core library as base and extended it with the protobuf.net as message protocol(proto server/client) and implemented higher level features such as P2P network with relay server and holepunching system. The generic server client model has both the SSL variant and regular variant, however P2P network is only done base on SSL variant so far. You can also use this library as a reference to see how the core library extends if you want to use your own protocol.

Both assemblies are written on .Net Standard 2.0. Nuget Packages are available:
- Core : [![NuGet](https://img.shields.io/nuget/v/Standard.Network.Library)](https://www.nuget.org/packages/Standard.Network.Library)
- Protobuf : [![NuGet](https://img.shields.io/nuget/v/Protobuf.Network.Library)](https://www.nuget.org/packages/Protobuf.Network.Library/)

## Features
### CoreLib features
- The core library is well optimised for small and high traffic messages such as for IoT applications.
- It emulates MQ systems and offers zero allocation and no extra byte copies.
- Offers built in Byte Message Server/Client model with byte message protocol with 4 byte size header. Sending any size of byte will reach the destination without fragmentation 
- Secure Udp Server/Client model with AES encyrption support.
- Interfaces and blueprints for extention without extra copy with few overrides, see the methodology on other assembly.
- Reusable Utility features such as concurrent Aes encryptor-decryptor without allocation overhead.

### Protobuf features
- Secure and rugular Protobuf message Server/Client models. Based on regular and encrypted Tcp And Udp servers on core libraries.
- P2P topology support using protobuf messages by Relay server & client model.
- Secure Udp holepunching on P2P network.

### Internal Architectural Features
- Weak Reference Global Shared Memory Pool where each byte array is rented and returned. Each memory user including memory stream backing buffers are rented though the pool.
Pool`s memory is automatically maintained for trimming by the GC itself. Protobuf serialisation also shares this pool. Memory footprint is low and scaleable and low GC pressure.

- System calls for socket send and recieve are expensive. When Tcp traffic is on high load, messages are stiched together to increase throughput, idea is to gather the messages and send in one system call. This is only for high load so there is no latency issue on regular traffic. This gather sysytem is configrable and can be based on queue or buffer stream swaps.

# Documentation
I will provide more extensive docs on related folders in the future..

# Sample Code 
## Base Byte Message TCP Server Client
Any chunk of byte array will reach the destination without fragmentation.
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

Infinite Echo benchmarks are done by sending set of messages to server and getting echo reply. Each round trip is counted as 1 echo. 
Benchmark programs are provided in the project.
Tests are done on my personal laptop with AMD Ryzen 7 5800H.

Benchmark results are as follows:
### Tcp Byte Message Server: 100 clients 1000 seed messages (32 bytes + 4 header) each:

~65,000,000 echo per second.

### SSL Byte message: 100 clients 1000 seed messages(32 bytes + 4 header) each:

~ 44,600,000 echo per second.

### Secure Protobuf Client Server 100 clients 1000 seed messages ( 32 byte payload 48 byte total):

~ 4,050,000 echo per second (4m ingress, 4m egress).
