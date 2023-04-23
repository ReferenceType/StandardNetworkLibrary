//using System;
//using System.Collections.Generic;

//namespace Protobuff.Components.TransportWrapper.Generic.Interfaces
//{
//    public interface IMessageEnvelope
//    {
//        Guid From { get; }
//        string Header { get; set; }
//        bool IsLocked { get; }
//        Dictionary<string, string> KeyValuePairs { get; set; }
//        Guid MessageId { get; set; }
//        byte[] Payload { get; set; }
//        int PayloadCount { get; }
//        int PayloadOffset { get; }
//        DateTime TimeStamp { get; set; }
//        Guid To { get; }
//        bool IsInternal { get; }

//        void LockBytes();
//        void SetPayload(byte[] payloadBuffer, int offset, int count);
//        T UnpackPayload<T>() where T : IProtoMessage;
//    }
//}
