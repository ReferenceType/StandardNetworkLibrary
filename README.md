# .Net Standard Network Library.
High Performance easy to use Network library supports 16k+ clients. 
</br>This library Consist of two assemblies:
 - Core Network Library, which is the is the core high performance library and works only with bytes. It offers extensibility with direct serialisation on any protocol. All features are designed from scracth. Assembly does not require any external package, you can use the dll directly. It also offers great utilites such as pooled memory streams. It supports up to 16k clients (beyond 16k client my system(windows 11 home) runs out of ports).

 - Protobuff Assembly where I used the core library as base and extended it with the protobuf.net as message protocol(proto server/client) and implemented higher level features such as P2P network with relay server/client model with NAT travelsal support like holepunching. The generic server Proto client model has both the SSL variant and regular variant, however Relay Server/Client P2P network is only implemented based on SSL variant so far. 

Both assemblies are written on .Net Standard 2.0. Nuget Packages are available:
- Core : [![NuGet](https://img.shields.io/nuget/v/Standard.Network.Library)](https://www.nuget.org/packages/Standard.Network.Library)
- Protobuf : [![NuGet](https://img.shields.io/nuget/v/Protobuf.Network.Library)](https://www.nuget.org/packages/Protobuf.Network.Library/)

## Features
### CoreLib features
- The core library is well optimised for small and high traffic messages.
- It emulates MQ systems and offers zero allocation and no extra byte copies.
- Offers standard TCP and SSL Client/Server model, where under high load, byte sends are compacted to increase throughput.
- Offers built in Byte Message Server/Client model (both TCP and SSL), implementing byte message protocol with 4 byte size header. Sending any size(up to 2GB) of byte[] or a segment, will reach the destination atomically without fragmentation.
- Offers Secure UDP Server/Client model with AES encyrption support.
- Offers Interfaces and abstractions for extention with few overrides, For maximum performance see the methodology on Protobuf assembly.
- Offers reusable Utility features such as concurrent Aes encryptor-decryptor without allocation overhead and pooled memory streams.

### Protobuf features
- Protobuf message Server/Client models where it can be based on SSL or standard TCP. It is extended ftom Tcp And Udp servers on core library.
- P2P topology support using protobuf messages with Relay Server/Client model.
- Secure NAT traversal support, Udp holepunching on P2P network.

### Internal Architectural Features
- Weak Reference Global Shared Memory Pool where each byte array is rented and returned. Each memory user including memory stream backing buffers are rented though the pool.
Pool`s memory is automatically maintained for trimming by the GC itself. Protobuf serialisation also shares this pool. Memory footprint is low and scaleable. This design reduces GC pressure significantly.

- System calls for socket send and recieve are expensive. When Tcp traffic is on high load, messages are stiched together to increase throughput, idea is to gather the messages and send in one system call. This system is automatically employed during high load only. Gathering system is configrable and can be based on queue or buffer stream swaps.

- I tried my best to produce maximum JIT optimisation on performance critical regions. 

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

# Sample Code 
## Base Byte Message TCP Server Client
Any chunk of byte array or array segment will reach the destination without fragmentation.
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
Proto Server/Client model is based on protobuf.net.
You can declare your payload types, Any type that can be serialised with protobuf, no limitations.
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
## Relay Server/Client and P2P

This model is what I personally use on my other projects such as P2P Videocall and multiplayer starfighter game.
Basically you have a Relay server somewhere in your network, which can be on a local network hub in LAN and/or open to connections from internet if port forwarding is enabled. 
### Relay server
Server is completely passive, allowing other peers to discover and send messages to each other. Additionally provides NAT traversal methods such as UDP holepunching to allow direct communication via Internet or LAN (UDP only so far).
<br/><br/> To use the Relay server, simply declere your server as:
``` c#
            var scert = new X509Certificate2("server.pfx", "greenpass");
            var server = new SecureProtoRelayServer(20010, scert);
```
Its done. If you want to see some statistics you can use the methods:
``` c#
            server.GetTcpStatistics(out TcpStatistics stats,
                                    out ConcurrentDictionary<Guid,TcpStatistics> statsPerSession);

            server.GetUdpStatistics(out UdpStatistics udpStats, 
                                    out ConcurrentDictionary<IPEndPoint, UdpStatistics> udpStatsPerSession);
                                    
            // Udp does not have session concept, but you can get the guid of the client with this method:
            server.TryGetClientId(ipEndpoint,out Guid clientId);
```
### Relay Client
Relay client is where your application logic is implemented. You can web your client applications to discover and talk with each other.
</br>To declere a client:
``` c#
            var cert = new X509Certificate2("client.pfx", "greenpass");
            var client = new RelayClient(cert);

            client.OnPeerRegistered += (Guid peerId) => { // Save it to some concurrent dictionary etc..};
            client.OnPeerUnregistered += (Guid peerId) => { };
            client.OnMessageReceived += (MessageEnvelope message) => { // Handle your messages, 
                                                                       // I use switch case on message.Header };
            client.OnUdpMessageReceived += (MessageEnvelope message) => { };

            client.Connect("127.0.0.1", 20010);
```
Sending messages are identical to proto client server model (also with Payloads).Only difference is you have to specify the destination peer Guid Id, which comes with OnPeerRegistered event:
``` c#
            client.SendAsyncMessage(destinationPeerId, new MessageEnvelope() { Header = "Hello" });
            
            // Or with an async reply
            MessageEnvelope response = await client.SendRequestAndWaitResponse(destinationPeerId,
                                                  new MessageEnvelope() { Header = "Who Are You?" });
```

Holepunch Support:
``` c#
           bool result = await client.RequestHolePunchAsync(destinationPeerId, timeOut:10000);
```
if succesfull, it will allow you to send direct udp messages between current and destination peers for the rest of the udp messages in both directions.


