# Standard Network Library
High Performance, easy to use network library supporting 16k+ concurrent clients on all provided topologies. 
</br>Designed for distributed realtime concurrent applications over LAN or Internet.
</br>Provides infrastructure for high throughput message passing, P2P, Nat Traversal, Reliable Udp.
</br>This repository consist of main core assembly and several serialization spesific sub assemblies. 

Please check out [Wiki](https://github.com/ReferenceType/StandardNetworkLibrary/wiki) page for detailed documentation.

## Core Network Library
 Network Library, Includes all logic associated with the network systems, starting from raw bytes to abstractions such as P2P lobbies. It provides generic templates to be used with any type of serialization. 

### Low Level
Plug&Play high performance models, working with raw bytes. Also used as base for higher level models.
- ```Tcp Server/Client model``` with dynamic buffering and queueing sub systems, bytes can come fragmented like regular sockets.
- ```Tcp Byte Message Server/Client``` where bytes are sent with 4 byte lenght header. It ensure atomic message delivery without fragmentation.
- ```Udp Server/Client``` udp system where a server is emulated with optimised for performance.
- ```Reliable Udp Client/Server``` where modern TCP protocol is implemented over Udp.
- Secure variants of all of the above(SSL with TLS for Tcp, Symetric Key AES for Udp).

### High Level
Involves generic models which can work with any serialization protocol.
- ```Generic Message Server/Client``` designed to send and receive serialized messages atomically.
- ```Generic MessageProtocol Server/client``` similar to above, but with addition of "MessageEnvelope" carrier class, used as header/metadata.
- ```P2P Relay Client/Server``` where Peers(Clients) discover each other via Relay server, can use Udp/Rudp/Tcp to communicate. Supports Tcp&Udp Holepunch.
- ```P2P Room/Lobby Server/Client``` extention of Relay model where peers can define a room, similar to game matchmaking servers.

### Internal Features
#### Message Buffering/Queueing
- Tcp models come with a buffering/queueing system. This system is activated only during high load.
In a nutsell, if the socket is busy sending, next messages are buffered/queued and stiched together. When the socket becomes available again, sent as batch. Its like Naggle, but without sacrificing the fast sends on moderate/low traffic.
- This improves the throughput of small messages quite significantly (1000 fold compared to naive sends) as shown on benchmarks.

#### Advanced Memory Management
- Library implements "Shared Thread Local Memory Pool", where all byte arrays and the stream backing buffers are rented from. 
- As an example, RelayServer can relay 21 Gigabyte/s Udp traffic between 1000 clients using 36 mb process memory with 0 GC Colections.

#### Build In Serialization
- Library provides high performance binary encoders for primitive and well known types and employs static srialization for internal message types and carrier classes with smallest possible byte size.

Library is tested with as many clients as OS(Windows) supports (around 16k dynamic ports). Data reliability includung RUDP is tested over the internet extensively.
Nat Traversal Udp holepunching is also tested over the internet with success.

Note: Libary has unsafe code and stack memory allocations. Unsafe sections are well tested and not subject to change.

## Sub Assemblies 
Generic models from main assembly are implemented with the spesific serializer.Reason for this division is to avoid unnecessary dependencies.
All method signatures and usage are identical.
It includes:
- Protobuf-Net
- MessagePack
- NetSerializer
- System.Text.Json
 
## Supported Runtimes
- .NET Standard 2.0+
- Up to .NET 8

Nuget Packages are available:
|Core Network Library| Protobuf|MessagePack |NetSeralizer|Json |
|---------------|---------------|---------------|---------------|---------------|
|[![NuGet](https://img.shields.io/nuget/v/Standard.Network.Library)](https://www.nuget.org/packages/Standard.Network.Library)| [![NuGet](https://img.shields.io/nuget/v/Protobuf.Network.Library)](https://www.nuget.org/packages/Protobuf.Network.Library/)|[![NuGet](https://img.shields.io/nuget/v/MessagePack.Network.Library)](https://www.nuget.org/packages/MessagePack.Network.Library)|[![NuGet](https://img.shields.io/nuget/v/NetSerializer.Network.Library)](https://www.nuget.org/packages/NetSerializer.Network.Library)|[![NuGet](https://img.shields.io/nuget/v/Json.Network.Library)](https://www.nuget.org/packages/Json.Network.Library)

# Benchmarks

Infinite Echo benchmarks are done by sending set of messages to server and getting echo response. Each response causes new request. Each server response is counted as 1 Echo. 
- Separate client and server applications are used for the tests.
- Tests are done on my personal laptop with CPU AMD Ryzen 7 5800H.
- Benchmark programs are provided in the project.

### TCP/SSL Byte Message Server 
1000 seed messages (32 bytes message + 4 header) each:

|Mumber Of Clients|TCP Echo per Second|SSL Echo per Second
|---|---|---|
|100|53,400,000|41,600,000|
|1000|43,600,000|22,200,000|
|5000|43,400,000|21,800,000|
|10000|42,800,000|21,700,000|

i9 13980HX Laptop
|Mumber Of Clients|TCP Echo per Second|SSL Echo per Second
|---|---|---|
|100|128,800,000|79,400,000|

### MessageProtocol 
1000 seed message envelopes ( 32 byte payload, 48 byte total):

|Mumber Of Clients|Protobuf Echo per Second|Secure Protobuf Echo per Second|
|---|---|---|
|100|9,440,000|8,050,000|
|1000|8,780,000|7,480,000|
|5000|8,360,000|7,390,000|
|10000|8,340,000|7,350,000|

i9 13980HX Laptop
|Mumber Of Clients|Protobuf Echo per Second|Secure Protobuf Echo per Second|
|---|---|---|
|100|30,200,000|20,650,000|

#### Note
This benchmarks is only sending message envelope with raw byte payload. For serialization spesific performance please refer to:
[SerializationBenchmarks](SerializationBenchmarks.md)


# Quick Documentation & Code Samples
## Byte Message TCP Server/Client
For Detailed info Check out [AsyncTcpClient/Server](https://github.com/ReferenceType/StandardNetworkLibrary/wiki/1.AsyncTcpClient-%E2%80%90-Server)
 and [ByteMessageTcpClient/Server](https://github.com/ReferenceType/StandardNetworkLibrary/wiki/2.ByteMessageTcpClient-%E2%80%90-Server)
 
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
 ```
```Console
output:
Hello I'm a client!
Hello I'm the server
```
Note: Important performance setting here is whether to use a buffer or a queue as buffering policy. Use Buffer if messages are mainly regions of byte[] such as (buffer, offset,count).
Use Queue if the messages are full byte[] (0 to end).
```c#
    client.GatherConfig = ScatterGatherConfig.UseQueue;
    server.GatherConfig = ScatterGatherConfig.UseBuffer;
```
</br>For SSL variants difference is:
```c#
   var ccert = new X509Certificate2("client.pfx", "greenpass");
   // null certificate or default constructor will generate self signed certificate
   client = new SslByteMessageClient(ccert);
 
   var scert = new X509Certificate2("server.pfx", "greenpass");
   // null certificate or default constructor will generate self signed certificate
   server = new SslByteMessageServer(8888,scert);
   
   // You can override the SSL cerificate validation callback
   server.RemoteCertificateValidationCallback+= ...
   client.RemoteCertificateValidationCallback+= ...
```
For more info check out [SSLClient/Server](https://github.com/ReferenceType/StandardNetworkLibrary/wiki/3.SSLClient-%E2%80%90-Server) And [SSLByteMessageClient/Server](https://github.com/ReferenceType/StandardNetworkLibrary/wiki/4.SslByteMessageClient%E2%80%90Server)

Base Server/Client where raw bytes are transfered. Method and callback signarures are identical to byte message models.
There is no protocol implemented over base Server/Client, hence bytes may come fragmented depending on your MTU size.
```c#
   AsyncTcpServer server = new AsyncTcpServer(port: 20000);
   AsyncTpcClient client = new AsyncTpcClient();
   
   // SSL variant
   // null certificate or default constructor will generate self signed certificate
   var ccert = new X509Certificate2("client.pfx", "greenpass");
   var scert = new X509Certificate2("server.pfx", "greenpass");
   SslServer server = new SslServer(2000, scert);
   SslClient client = new SslClient(ccert);
```

## Serialized Networks
Serialized Networks are implementations of generic classes provided by Core Library
It is applicable to all serialization protocols. 

For more info : [Serialised Network](https://github.com/ReferenceType/StandardNetworkLibrary/wiki/5.SerialisedNetwork)
</br>```Examples here is only given for Protobuf-net,
but signature is identical for any other provided serialization protocol(MessagePack, Json etc)```.
### Protobuf Example
Implements a server client model where serialized messages are transfered atomically.
Declare your type:
```c#
    [ProtoContract]
    class SampleMessage
    {
        [ProtoMember(1)]
        public string sample;
    }
```
```c#
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
```
## MessageProtocol Server/Client

Message protocol is implemented for wrapping all dynamic message types with a standard header.
Please refer to [Message Protocol](https://github.com/ReferenceType/StandardNetworkLibrary/wiki/6.MessageProtocol)
for detailed explanation.
 
You can declare your payload types, which any type that is serializable with protobuf.
```c#
        [ProtoContract]
        class SamplePayload :IProtoMessage
        {
            [ProtoMember(1)]
            public string sample;
        }
```
Example for the Secure Variant:
``` c#
  private static async Task ExampleProtoSecure()
        {
            // null certificate or default constructor will generate self signed certificate
            var scert = new X509Certificate2("server.pfx", "greenpass");
            var cert = new X509Certificate2("client.pfx", "greenpass");

            SecureProtoMessageServer server = new SecureProtoMessageServer(20008, scert);
            server.StartServer();
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
```
- Unsecure variants ``` ProtoMessageServer ``` and ``` ProtoMessageClient ``` has identical signarures except the constructors doesnt take a ceritificate

## Relay Server/Client and P2P

This model is what I personally use on my other projects such as P2PVideocall and Multiplayer Starfighter Game.
Basically you have a Relay server somewhere in your network, which can act as a local network hub in LAN and/or open to connections from internet if port forwarding is enabled. 
<br/>Relay clients (Peers) connect to Relay server and get notifications about existince of other peers. Peers can send messages to each other through Relay Server, or directly to each other (Udp holepunch).

Check out [P2P](https://github.com/ReferenceType/StandardNetworkLibrary/wiki/7.P2P) Fore detailed info

## Relay server
Server is completely passive, allowing other peers to discover and send messages to each other. Additionally NAT traversal methods such as UDP holepunching provided to allow direct communication via Internet or LAN (UDP only so far, but we have reliable udp).

```Relay Server Is Serialization Agnostic``` which means any serialized network Peers, (Protobuff, MessagePack etc) can use the same relay server.
<br/> To use the Relay server, simply declere your server as:
``` c#
      var scert = new X509Certificate2("server.pfx", "greenpass");
      var server = new SecureProtoRelayServer(20010, scert);
      server.StartServer();
```
Relay server is already pre-configured.

## Relay Client
Relay client is where your application logic is implemented. You can web your client applications to discover and talk with each other.
</br>To declere a client:
``` c#
     // null certificate or default constructor will generate self signed certificate
      var cert = new X509Certificate2("client.pfx", "greenpass");
      var client = new RelayClient(cert);

      client.OnPeerRegistered += (Guid peerId) => ..
      client.OnPeerUnregistered += (Guid peerId) => ..
      client.OnMessageReceived += (MessageEnvelope message) => .. 
      client.OnUdpMessageReceived += (MessageEnvelope message) => ..
      client.OnDisconnected += () => ..

      client.Connect("127.0.0.1", 20010);
```
Method signatures and callbacks are identical to proto client/server model (also with Payloads). Only difference is you have to specify the destination peer Guid Id. It comes from OnPeerRegistered event, whenever a new peer is connected to relay server. Relay Server guaranties syncronisation of current peer set with eventual consistency among all peers. So new peers will receive all other connected peers from this event and old peers will receive an update.
``` c#
      client.SendAsyncMessage(destinationPeerId, new MessageEnvelope() { Header = "Hello" });
      client.SendUdpMesssage(destinationPeerId, new MessageEnvelope() { Header = "Hello" });
      // Or with an async reply
      MessageEnvelope response = await client.SendRequestAndWaitResponse(destinationPeerId,
                                            new MessageEnvelope() { Header = "Who Are You?" });
```

Udp messages can be more than the datagram limit of 65,527 bytes. The system detects large udp messages as Jumbo messages and sends them in chunks. Receiving end with will try to reconstruct the message. if all the parts does not arrive within a timeout message is dropped.
Max message size for udp is 16,256,000 bytes.

Reliable udp protocol uses as TCP algorithm implemented over UDP. 
```c#
      client.SendRudpMessage(peerId, envelope);
      client.SendRudpMessage(peerId, envelope, innerMessage);
      client.SendRudpMessageAndWaitResponse(peerId, envelope, innerMessage);
      client.SendRudpMessageAndWaitResponse(peerId, envelope);
```

Nat Traversal/Holepunch Support:
``` c#
      // Udp
      bool result = await client.RequestHolePunchAsync(destinationPeerId, timeOut:10000);
      // Tcp
      bool result = await client.RequestTcpHolePunchAsync(destinationPeerId, timeOut:10000);
```
if succesfull, it will allow you to send direct udp messages between current and destination peers for the rest of the udp messages in both directions.

## Room/Lobby Server
This is an extention of Relay Server/Client. The addition is the room system where peers can create or join rooms, query available rooms, sends message to rooms(multicast).
Additionally keeping same message system to send 1-1 messages among peers.

You can join multiple rooms

```Room Server Is Serialization Agnostic``` which means any serialized network Peers, (Protobuf, MessagePack etc) can use the same Room server.


Decleration of Server and client
``` c#
     var server = new SecureProtoRoomServer(20010, scert);
     server.StartServer();

     var client1 = new SecureProtoRoomClient(cert);
```
To create/join and leave rooms simply:
```c#
     client1.CreateOrJoinRoom("Kitchen");
     client1.LeaveRoom("Kitchen");

```
Room callbacks are as followed. This callbacks are only triggered if you are in the same room.
```c#
    client1.OnPeerJoinedRoom += (roomName, peerId) =>..
    client1.OnPeerLeftRoom += (roomName, peerId) =>..
    client1.OnPeerDisconnected +=(peerId) =>..
```
Additionally to the standard 1-1 message callback we have room message callbacks.
```c#
    client1.OnTcpRoomMesssageReceived += (roomName, message) => ..
    client1.OnUdpRoomMesssageReceived += (roomName, message) => ..
    client1.OnTcpMessageReceived += (message) => ..
    client1.OnUdpMessageReceived += (message) => ..

```
## P2P Remarks
- Relay and lobby servers only uses MessageEnvelope and staticly serialized internal message types to communicate and provide functionalities, Therefore any Room client or Relay client, independent of what type of serialization they use, can acces and use all functionalities simultaneously.
- Peers with different serialization protocols can talk with each other. They can send MessageEnvelope and Raw bytes without a problem. Understanding which protocol peer uses should be defined in application which is up to the user. Hence their serialized inner messages can be deserialized from envelope payload bytes accordingly.

# Cyber Security
All secure TCP variants are implementing standard SSL socket with TLS authentication/validation.
For more info check out : [Security](https://github.com/ReferenceType/StandardNetworkLibrary/wiki/8.Security)
