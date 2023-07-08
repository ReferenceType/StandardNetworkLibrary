using NetworkLibrary.MessageProtocol;
using NetworkLibrary.Utils;
using ProtoBuf;
using System;
using System.Collections.Generic;

namespace ProtobufNetwork
{

    // this is mini version of envelope to route messages more efficiently.
    // message envelope can be deserialized into this.
    [ProtoContract]
    internal struct RouterHeader : IRouterHeader
    {
        [ProtoMember(5)]
        public Guid To { get; set; }

        [ProtoMember(7)]
        public bool IsInternal { get; set; }
    }

    // size cant be more than 64kb
    [ProtoContract]
    public class MessageEnvelope : IMessageEnvelope
    {
        public static GenericMessageSerializer<MessageEnvelope, ProtoSerializer> serialiser
            = new GenericMessageSerializer<MessageEnvelope, ProtoSerializer>();
        public const string RequestTimeout = "RequestTimedOut";
        public const string RequestCancelled = "RequestCancelled";

        [ProtoMember(1)]
        public DateTime TimeStamp { get; set; }

        [ProtoMember(2)]
        public Guid MessageId { get; set; }

        [ProtoMember(3)]
        public string Header { get; set; }

        [ProtoMember(4)]
        public Guid From { get; set; }

        [ProtoMember(5)]
        public Guid To { get; set; }

        [ProtoMember(6)]
        public Dictionary<string, string> KeyValuePairs { get; set; }

        [ProtoMember(7)]
        public bool IsInternal { get; set; }
        public bool IsLocked { get; private set; } = true;

        private byte[] payload;
        public byte[] Payload
        {
            get => payload; set
            {
                if (value == null)
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
            }
        }
        public int PayloadOffset { get; private set; }
        public int PayloadCount { get; private set; }

        public void SetPayload(byte[] payloadBuffer, int offset, int count)
        {
            if (count == 0) return;
            Payload = payloadBuffer;
            PayloadOffset = offset;
            PayloadCount = count;
        }
        /// <summary>
        /// Desiralises the payload bytes into given type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T UnpackPayload<T>()
        {
            return serialiser.UnpackEnvelopedMessage<T>(this);
        }
        /// <summary>
        /// Locks the payload bytes by making a copy.
        /// You Must Lock the bytes or unpack payload if message will leave the stack.
        /// Payload will be overwitten on next message
        /// </summary>
        public void LockBytes()
        {
            if (payload == null || IsLocked)
                return;
            IsLocked = true;
            Payload = ByteCopy.ToArray(Payload, PayloadOffset, PayloadCount);
            PayloadOffset = 0;
            PayloadCount = Payload.Length;
            IsLocked = false;
        }

    }
}
