using NetworkLibrary.MessageProtocol;
using NetworkLibrary.P2P.Components;
using NetworkLibrary.Utils;
using System;
using System.Collections.Generic;

namespace NetworkLibrary
{
    public class MessageEnvelope : IMessageEnvelope
    {
        public const string RequestTimeout = "RequestTimedOut";
        public const string RequestCancelled = "RequestCancelled";
        public static IMessageSerialiser Serializer
        {
            get => serializer;
            internal set
            {
                if (serializer == null && value is GenericMessageSerializer<MockSerializer>)
                    return;
                serializer = value;
            }
        }
        public DateTime TimeStamp { get; set; }

        public Guid MessageId { get; set; }

        public string Header { get; set; }

        public Guid From { get; set; }

        public Guid To { get; set; }

        public Dictionary<string, string> KeyValuePairs { get; set; }

        public bool IsInternal { get; set; }

        public bool IsLocked { get; private set; } = true;

        private byte[] payload;
        private static IMessageSerialiser serializer;

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
            return Serializer.UnpackEnvelopedMessage<T>(this);
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

        internal static MessageEnvelope CloneWithNoRouter(MessageEnvelope message)
        {
            var msg = new MessageEnvelope();
            msg.Header = message.Header;
            msg.MessageId = message.MessageId;
            msg.KeyValuePairs = message.KeyValuePairs;
            msg.IsInternal = message.IsInternal;
            msg.TimeStamp = message.TimeStamp;
            msg.Payload = message.Payload;
            msg.PayloadOffset = message.PayloadOffset;
            msg.PayloadCount = message.PayloadCount;
            return msg;

        }

    }

}



