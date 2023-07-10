# Message Protocol
Considering a client/ server model, where a type is serialized and sent over the network. In order to infer what the bytes represent on receiving end, 
we need to have some sort of metadata about the message. 
```c#
    server.BytesReceived += (clientId,bytes, offset, count) => 
    {
       // What did I receive??
    };

    PureProtoClient client = new PureProtoClient();
    client.Connect("127.0.0.1", 11234);
    client.SendAsync(new SampleMessage() { sample = "Yo! Mr White" });
    
```
Because of this, I have implemented an envelope class which acts as a metadata/header/carrier on top of an optional payload. Intention here is to be reusable on all network systems included in the library.
The message envelope roughly looks as follows:
```c#
 public class MessageEnvelope : IMessageEnvelope
    {
        public bool IsInternal {get; set;}
        public DateTime TimeStamp { get; set; }
        public Guid MessageId { get; set; }
        public string Header { get; set; }
        public Guid From { get; set; }
        public Guid To { get; set; }
        public Dictionary<string, string> KeyValuePairs { get; set; }
 }
```
Where any of the properties can be null/default and they wont be considered on serialization. You only send what you wrote.

Core network system identifies each session with a GUID.
This Guid is also represents a ClientId for the server side of the TCP systems. in P2P systems such as Relay P2P or Room server this Guid identifies PeerId .
Hence the ```From``` and ```To``` properties represens from peer to another peer on P2P cases.

```MessageId``` is used for async calls like  ```MessageEnvelope response = await SendMessageAndWaitResponse(..., timeoutMs:10000)```
when such method is called a MessageId is automatically assigned.
Once the receiving end receives a message with a ```MessageId``` and sends a reply message with same Id, it will be caught by the sender will become the async response. its up to the developer how to filder such messages.

Note that when such async call is active/pending socket is not blocked, you can send as many messages as you want parallelly in between.

## Payload
Payload is essentially an index for some bytes. 
 ### 1. Raw bytes
```c#
        MessageEnvelope envelope = new MessageEnvelope()
        {
            Header = "ImageTransfer",
            Payload = bytes,
        };

        // For byte segments, there is no extra copy here.
        envelope.SetPayload(Buffer, offset, count);
```
when sent through a network system this bytes will reach to its destination atomically.

### 2. A serialized message
considering a method such as
```  client.SendAsyncMessage(messageEnvelope, innerMessage); ``` The inner message will be serialized as byte payload.
Receiving end is responsible to deserialize from the information provided by the message envelope.

``` c#
OnMessageReceived(envelope)
{
    SomeDataClass innerMesssage = envelope.UnpackPayload<SomeDataClass>()
}
```

## Remarks
- MessageEnvelope and some internal messages are serialized statically with efficient binary encoding. See [SerializationBenchmarks](SerializationBenchmarks.md)
- On receiving end, payload bytes are volatile. If the execution leaves the stack, bytes may be overwitten by next message. If you intend to store payload, either copy manually or call LockBytes() method of the message envelope.
Unpacking / deserializing a message is safe since it inheritly does a copy.
- Using message envelope without an inner message for simple information transfer or raw data transfer is way more efficient (3x) and reccomended.
