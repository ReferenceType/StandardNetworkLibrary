# Standard Network Library
High Performance, easy to use, network library supporting 16k+ clients. 
</br>This library Consist of two assemblies:
 - Core Network Library, which is the base high performance network library, and works only with bytes. It offers extensibility with direct serialisation on any protocol. All features are designed from scracth. Assembly does not have any any external package dependency. It also offers great utilites such as pooled memory streams. It supports up to 16k clients, beyond 16k client my system(windows 11 home) runs out of ports.

 - Protobuff Assembly where I used the core library as base and extended using the protobuf.net for serialization and implemented a message protocol (Proto Server/Client). Additionally implemented higher level features such as P2P network with Relay Server/Client model supporting NAT travelsal such as holepunching. Proto Server/Client model has both the SSL and unencrypted variant. Relay Server/Client P2P network is only implemented based on SSL Proto variant so far. 
 
## Supported Runtimes
- .NET Standard 2.0+

Nuget Packages are available:
|Core Netorwk Library| Protobuf Network Library|
|-------------------|-------------------------|
|    [![NuGet](https://img.shields.io/nuget/v/Standard.Network.Library)](https://www.nuget.org/packages/Standard.Network.Library)| [![NuGet](https://img.shields.io/nuget/v/Protobuf.Network.Library)](https://www.nuget.org/packages/Protobuf.Network.Library/)|

## Features
### CoreLib features
- The core library is well optimised for small and high traffic messages.
- It emulates MQ systems and offers zero allocation with no extra byte copies.
- Standard TCP and SSL Client/Server model, where under high load, byte sends are compacted to increase throughput.
- Built in "Byte Message" Server/Client model (both TCP and SSL), implementing byte message protocol with 4 byte size header. Sending any size(up to 2GB) of byte[] or a segment, will reach the destination atomically without fragmentation.
- Secure UDP Server/Client model with AES encyrption support.
- Interfaces and abstractions for extention with few overrides. For maximum performance, see the extension methodology on Protobuf assembly.
- Utility features such as Concurrent Aes encryptor-decryptor without allocation overhead with pooled memory streams.

### Protobuf features
- Protobuf message Server/Client models where it can be based on SSL or standard TCP. It is extended ftom Tcp And Udp servers on core library.
- P2P topology support using protobuf messages with Relay Server/Client model.
- Secure NAT traversal support, Udp holepunching on P2P network.

### Internal Architectural Features
- Weak Reference Global Shared Memory Pool where each byte array is rented and returned. Each memory user including memory stream backing buffers are rented though the pool.
Pool`s memory is automatically maintained for trimming by the GC itself. Protobuf serialisation also shares this pool. Memory footprint is low and scaleable. This design reduces GC pressure significantly.

- System calls for socket send and recieve are expensive. When Tcp traffic is on high load, messages are stiched together to increase throughput, idea is to gather the messages and send in one system call. This system is automatically employed during high load only. Gathering system is configrable and can be based on queue or buffer stream swaps.

- Best efford is put to produce maximum JIT optimisation on performance critical regions.

# Benchmarks

Infinite Echo benchmarks are done by sending set of messages to server and getting echo response. Each response causes new request. Each server response is counted as 1 Echo. 
- Separate client and server applications are used for the tests.
- Tests are done on my personal laptop with CPU AMD Ryzen 7 5800H.
- Benchmark programs are provided in the project.

Benchmark results are as follows:
### TCP/SSL Byte Message Server 
1000 seed messages (32 bytes message + 4 header) each:

|Mumber Of Clients|TCP Echo per Second|SSL Echo per Second
|---|---|---|
|100|53,400,000|41,600,000|
|1000|43,600,000|22,200,000|
|5000|43,400,000|21,800,000|
|10000|42,800,000|21,700,000|

### Protobuf Server 
1000 seed proto messages ( 32 byte payload, 48 byte total):

|Mumber Of Clients|Protobuf Echo per Second|Secure Protobuf Echo per Second|
|---|---|---|
|100|4,440,000|4,050,000|
|1000|4,380,000|3,980,000|
|5000|4,360,000|3,950,000|
|10000|4,340,000|3,890,000|

### Notes
As the client number increases (1000+) message throughput fluctuates, i.e. Protobuf throughput goes between 6m to 2m, Results are averaged.


# Code Samples
## Core Library
## Base Byte Message TCP Server Client
Any chunk of byte array or array segment will reach the destination without fragmentation.
```csharp
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
 ```
```Console
output:
Hello I'm a client!
Hello I'm the server
```
</br>For SSL variants only difference is:
```c#
   var ccert = new X509Certificate2("client.pfx", "greenpass");
   client = new SslByteMessageClient(ccert);
 
   var scert = new X509Certificate2("server.pfx", "greenpass");
   server = new SslByteMessageServer(8888,scert);
   
   // You can override the SSL cerificate validation callback
   server.RemoteCertificateValidationCallback+= ...
   client.RemoteCertificateValidationCallback+= ...
```
For base Server/Client where raw bytes are transfered you can use following classes. Method and callback signarures are identical to byte message models.
```c#
   AsyncTcpServer server = new AsyncTcpServer(port: 20000);
   AsyncTpcClient client = new AsyncTpcClient();
   
   // SSL variant
   var ccert = new X509Certificate2("client.pfx", "greenpass");
   var scert = new X509Certificate2("server.pfx", "greenpass");
   SslServer server = new SslServer(2000, scert);
   SslClient client = new SslClient(ccert);
```
There is no protocol implemented over base Server/Client described above, so bytes may come fragmented depending on your MTU size.
## Protobuf
## Secure Proto Client Server
Proto Server/Client model is based on protobuf.net.
You can declare your payload types, which any type that is serializable with protobuf.
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

This model is what I personally use on my other projects such as P2PVideocall and Multiplayer Starfighter Game.
Basically you have a Relay server somewhere in your network, which can act as a local network hub in LAN and/or open to connections from internet if port forwarding is enabled. 
<br/>Relay clients (Peers) connect to Relay server and get notifications about existince of other peers. Peers can send messages to each other through Relay Server, or directly to each other (Udp holepunch).
<br/><img src="https://user-images.githubusercontent.com/109621184/204115163-3c8da2c3-9030-4325-9f4a-28935ed98977.png" width=50% height=50%>
### Relay server
Server is completely passive, allowing other peers to discover and send messages to each other. Additionally provides NAT traversal methods such as UDP holepunching to allow direct communication via Internet or LAN (UDP only so far).
<br/> To use the Relay server, simply declere your server as:
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
      client.OnDisconnected += () => { };

      client.Connect("127.0.0.1", 20010);
```
Sending messages and method signatures are identical to proto client/server model (also with Payloads). Only difference is you have to specify the destination peer Guid Id, which comes from OnPeerRegistered event whenever a new peer is connected to relay server. Relay Server guaranties syncronisation of current peer set with eventual consistency among all peers. So new peers will receive all other connected peers from this event and old peers will receive an update.
``` c#
      client.SendAsyncMessage(destinationPeerId, new MessageEnvelope() { Header = "Hello" });
      client.SendUdpMesssage(destinationPeerId, new MessageEnvelope() { Header = "Hello" });
      // Or with an async reply
      MessageEnvelope response = await client.SendRequestAndWaitResponse(destinationPeerId,
                                            new MessageEnvelope() { Header = "Who Are You?" });
```

Holepunch Support:
``` c#
      bool result = await client.RequestHolePunchAsync(destinationPeerId, timeOut:10000);
```
if succesfull, it will allow you to send direct udp messages between current and destination peers for the rest of the udp messages in both directions.


