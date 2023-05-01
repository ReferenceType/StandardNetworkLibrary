using System;
using System.Collections.Generic;

namespace NetworkLibrary.MessageProtocol
{
    public interface IMessageEnvelope
    {
        Guid From { get; set; }
        string Header { get; set; }
        bool IsLocked { get;  }
        Dictionary<string, string> KeyValuePairs { get; set; }
        Guid MessageId { get; set; }
        byte[] Payload { get; set; }
        int PayloadCount { get; }
        int PayloadOffset { get;  }
        DateTime TimeStamp { get; set; }
        Guid To { get; set; }
        bool IsInternal { get; set; }

        void LockBytes();
        void SetPayload(byte[] payloadBuffer, int offset, int count);
        T UnpackPayload<T>();
    }
}
