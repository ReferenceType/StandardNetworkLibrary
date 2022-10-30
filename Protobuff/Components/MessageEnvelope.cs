using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Protobuff
{
    
    // lenght cant be more than 64kb
    [ProtoContract]
    public class MessageEnvelope
    {
        public const string RequestTimeout = "RequestTimedOut";
        public const string RequestCancelled = "RequestCancelled";
        //[ProtoContract]
        //public enum MessageTypes
        //{
        //    Status,
        //    Request,
        //    Response
        //}

        //[ProtoMember(1)]
        //public MessageTypes MessageType { get; internal set; }

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

    }
}
