using NetworkLibrary.MessageProtocol;
using NetworkLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JsonMessageNetwork.Components
{
    [JsonSerializable(typeof(MessageEnvelope))]
    public class MessageEnvelope : IMessageEnvelope
    {
        [JsonIgnore]
        public static GenericMessageSerializer<MessageEnvelope, JsonSerializer> serialiser
           = new GenericMessageSerializer<MessageEnvelope, JsonSerializer>();

        [JsonIgnore]
        public const string RequestTimeout = "RequestTimedOut";

        [JsonIgnore]
        public const string RequestCancelled = "RequestCancelled";

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public DateTime TimeStamp { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Guid MessageId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Header { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Guid From { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Guid To { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, string> KeyValuePairs { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsInternal { get; set; }

        [JsonIgnore]
        public bool IsLocked { get; set; } = true;

        [JsonIgnore]
        private byte[] payload;

        [JsonIgnore]
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

        [JsonIgnore]
        public int PayloadOffset { get; private set; }

        [JsonIgnore]
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
