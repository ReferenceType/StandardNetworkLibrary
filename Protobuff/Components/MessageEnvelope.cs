using NetworkLibrary.Utils;
using ProtoBuf;
using Protobuff.Components;
using System;
using System.Collections.Generic;
using System.Text;

namespace Protobuff
{
    // this is mini version of envelope to route messages more efficiently.
    // message envelope can be deserialized into this.
    [ProtoContract]
    internal struct RouterHeader
    {
        [ProtoMember(4)]
        public Guid From { get; internal set; }

        [ProtoMember(5)]
        public Guid To { get; internal set; }

        [ProtoMember(7)]
        public bool IsInternal;
    }

    // size cant be more than 64kb
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

        [ProtoMember(7)]
        internal bool IsInternal;
        public bool IsLocked { get; private set; } = true;

        private byte[] payload;
        public byte[] Payload { get=>payload;  set {
                if(value == null)
                {
                    PayloadOffset = 0;
                    PayloadCount = 0;
                    payload = null;
                }
                else
                {
                    payload = value;
                    PayloadOffset = 0;
                    PayloadCount = value.Length;
                    IsLocked = false;
                }
            } }
        public int PayloadOffset { get; private set; }
        public int PayloadCount { get; private set; }

        public void SetPayload(byte[] payloadBuffer, int offset, int count)
        {
            if (count == 0) return;
            Payload = payloadBuffer;
            PayloadOffset = offset;
            PayloadCount = count;
        }

        public T UnpackPayload<T>() where T : IProtoMessage
        {
            return serialiser.UnpackEnvelopedMessage<T>(this);
        }

        public void LockBytes()
        {
            if (payload == null || IsLocked)
                return;
            IsLocked= true;
            Payload = ByteCopy.ToArray(Payload, PayloadOffset, PayloadCount);
            PayloadOffset= 0;
            PayloadCount= Payload.Length;
            IsLocked = false;
        }

    }
}
