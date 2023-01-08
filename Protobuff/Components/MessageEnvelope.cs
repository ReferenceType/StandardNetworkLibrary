using ProtoBuf;
using Protobuff.Components;
using System;
using System.Collections.Generic;
using System.Text;

namespace Protobuff
{
    
    // lenght cant be more than 64kb
    [ProtoContract]
    public class MessageEnvelope 
    {
        public static ConcurrentProtoSerialiser serialiser= new ConcurrentProtoSerialiser();
        public const string RequestTimeout = "RequestTimedOut";
        public const string RequestCancelled = "RequestCancelled";

        [ProtoMember(1)]
        public DateTime TimeStamp { get; set; }

        [ProtoMember(2)]
        public Guid MessageId { get; set; }

        [ProtoMember(3)]
        public string Header { get; set; }

        [ProtoMember(4)]
        public Guid From { get; internal set; }

        [ProtoMember(5)]
        public Guid To { get; internal set; }

        [ProtoMember(6)]
        public Dictionary<string, string> KeyValuePairs { get; set; }

        public byte[] Payload { get; set; }

        public T UnpackPayload<T>() where T : IProtoMessage
        {
            return serialiser.UnpackEnvelopedMessage<T>(this);
        }

    }
}
