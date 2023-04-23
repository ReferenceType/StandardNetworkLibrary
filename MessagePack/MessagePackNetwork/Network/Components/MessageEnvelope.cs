using MessagePack;
using MessageProtocol;
using NetworkLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace MessagePackNetwork.Network.Components
{
    [MessagePackObject]
    public class MessageEnvelope : IMessageEnvelope
    {
        [IgnoreMember]
        public static GenericMessageSerializer<MessageEnvelope, MessagePackSerializer_> serialiser
            = new GenericMessageSerializer<MessageEnvelope, MessagePackSerializer_>();
        [IgnoreMember]
        public const string RequestTimeout = "RequestTimedOut";
        [IgnoreMember]
        public const string RequestCancelled = "RequestCancelled";

        [Key(1)]
        public DateTime TimeStamp { get; set; }

        [Key(2)]
        public Guid MessageId { get; set; }

        [Key(3)]
        public string Header { get; set; }

        [Key(4)]
        public Guid From { get; internal set; }

        [Key(5)]
        public Guid To { get; internal set; }

        [Key(6)]
        public Dictionary<string, string> KeyValuePairs { get; set; }

        [Key(7)]
        public bool IsInternal { get; internal set; }

        [IgnoreMember]
        public bool IsLocked { get; private set; } = true;

        private byte[] payload;

        [IgnoreMember]
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

        [IgnoreMember]
        public int PayloadOffset { get; private set; }

        [IgnoreMember]
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
