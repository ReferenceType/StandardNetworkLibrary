using NetworkLibrary.MessageProtocol;
using NetworkLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace NetSerializerNetwork.Components
{
    [Serializable]
    public class MessageEnvelope : IMessageEnvelope
    {
        [NonSerialized]
        public static GenericMessageSerializer<MessageEnvelope, NetSerialiser> serialiser
            = new GenericMessageSerializer<MessageEnvelope, NetSerialiser>();
        [NonSerialized]
        public const string RequestTimeout = "RequestTimedOut";
        [NonSerialized]
        public const string RequestCancelled = "RequestCancelled";


        public DateTime TimeStamp { get; set; }


        public Guid MessageId { get; set; }


        public string Header { get; set; }


        public Guid From { get; set; }


        public Guid To { get; set; }


        public Dictionary<string, string> KeyValuePairs { get; set; }


        public bool IsInternal { get; set; }

        [IgnoreDataMember]
        public bool IsLocked { get => isLocked; set => isLocked = value; }

        [NonSerialized]
        private byte[] payload;
        [NonSerialized]
        private int payloadOffset;
        [NonSerialized]
        private int payloadCount;
        [NonSerialized]
        private bool isLocked = true;

        [IgnoreDataMember]
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

        [IgnoreDataMember]
        public int PayloadOffset { get => payloadOffset; private set => payloadOffset = value; }

        [IgnoreDataMember]
        public int PayloadCount { get => payloadCount; private set => payloadCount = value; }

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
