using NetworkLibrary.MessageProtocol;
using NetworkLibrary.Utils;
using System;
using System.Collections.Generic;

namespace MessageProtocol.Serialization
{
    public class MessageEnvelope:IMessageEnvelope
    {
        public const string RequestTimeout = "RequestTimedOut";
        public const string RequestCancelled = "RequestCancelled";
        public static IMessageSerialiser Serializer { get; internal set; }
        public DateTime TimeStamp { get; set; }

        public Guid MessageId { get; set; }

        public string Header { get; set; }

        public Guid From { get; set; }

        public Guid To { get; set; }

        public Dictionary<string, string> KeyValuePairs { get; set; }

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

    }

    }



