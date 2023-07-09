using NetworkLibrary.MessageProtocol;
using NetworkLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace DataContractNetwork.Components
{

    [DataContract(Namespace = "")]
    public class MessageEnvelope : IMessageEnvelope
    {
        [IgnoreDataMember]
        public static GenericMessageSerializer<MessageEnvelope, DataContractSerialiser> serialiser
           = new GenericMessageSerializer<MessageEnvelope, DataContractSerialiser>();

        [IgnoreDataMember]
        public const string RequestTimeout = "RequestTimedOut";

        [IgnoreDataMember]
        public const string RequestCancelled = "RequestCancelled";

        [DataMember(EmitDefaultValue = false, IsRequired = false, Order = 1)]
        public DateTime TimeStamp { get; set; }

        [DataMember(EmitDefaultValue = false, IsRequired = false, Order = 1)]
        public Guid MessageId { get; set; }

        [DataMember(EmitDefaultValue = false, IsRequired = false, Order = 1)]
        public string Header { get; set; }

        [DataMember(EmitDefaultValue = false, IsRequired = false, Order = 1)]
        public Guid From { get; set; }

        [DataMember(EmitDefaultValue = false, IsRequired = false, Order = 1)]
        public Guid To { get; set; }

        [DataMember(EmitDefaultValue = false, IsRequired = false, Order = 1)]
        public Dictionary<string, string> KeyValuePairs { get; set; }

        [DataMember(EmitDefaultValue = false, IsRequired = false, Order = 1)]
        public bool IsInternal { get; set; }

        [IgnoreDataMember]
        public bool IsLocked { get; set; } = true;

        [IgnoreDataMember]
        private byte[] payload;

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
        public int PayloadOffset { get; private set; }

        [IgnoreDataMember]
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
