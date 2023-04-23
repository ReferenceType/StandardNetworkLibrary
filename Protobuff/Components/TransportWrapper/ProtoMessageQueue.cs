using MessageProtocol;
using Protobuff.Components.Serialiser;
namespace Protobuff.Components
{

    public class ProtoMessageQueue : GenericMessageQueue<ProtoSerializer, MessageEnvelope>
    {

        // private ProtoSerializer Serializer = new ProtoSerializer();
        public ProtoMessageQueue(int maxIndexedMemory, bool writeLengthPrefix = true) : base(maxIndexedMemory, writeLengthPrefix)
        {

        }

        //public bool TryEnqueueMessage<U,T>(U envelope, T message) where U : IMessageEnvelope
        //{
        //    lock (loki)
        //    {
        //        if (Volatile.Read(ref currentIndexedMemory) < MaxIndexedMemory && !disposedValue)
        //        {
        //            TotalMessageDispatched++;

        //            // offset 4 for lenght prefix (reserve)
        //            var originalPos = writeStream.Position;
        //            writeStream.Position += 4;

        //            SerializeMessageWithInnerMessage(writeStream, envelope, message);

        //            int msgLen = (int)(writeStream.Position - (originalPos + 4));
        //            var msgLenBytes = BitConverter.GetBytes(msgLen);

        //            // write the message lenght on reserved field
        //            var lastPos = writeStream.Position;
        //            writeStream.Position = originalPos;
        //            writeStream.Write(msgLenBytes, 0, 4);
        //            writeStream.Position = lastPos;
        //            Interlocked.Add(ref currentIndexedMemory, msgLen + 4);

        //            return true;

        //        }
        //    }
        //    return false;
        //}

        //public bool TryEnqueueMessage<E>(E envelope) where E : IMessageEnvelope
        //{

        //    lock (loki)
        //    {
        //        if (Volatile.Read(ref currentIndexedMemory) < MaxIndexedMemory && !disposedValue)
        //        {
        //            TotalMessageDispatched++;

        //            var originalPos = writeStream.Position;
        //            writeStream.Position += 4;
        //            SerializeMessage(writeStream, envelope);

        //            int msgLen = (int)(writeStream.Position - (originalPos + 4));
        //            var msgLenBytes = BitConverter.GetBytes(msgLen);

        //            var lastPos = writeStream.Position;
        //            writeStream.Position = originalPos;
        //            writeStream.Write(msgLenBytes, 0, 4);

        //            writeStream.Position = lastPos;
        //            Interlocked.Add(ref currentIndexedMemory, msgLen + 4);

        //            return true;

        //        }
        //    }
        //    return false;
        //}

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //internal void SerializeMessageWithInnerMessage<E,T>(Stream serialisationStream, E empyEnvelope, T innerMsg) where E : IMessageEnvelope
        //{
        //    // envelope+2
        //    var originalPos = serialisationStream.Position;

        //    serialisationStream.Position = originalPos + 2;
        //    Serializer.Serialize(serialisationStream, empyEnvelope);

        //    if (serialisationStream.Position - originalPos >= ushort.MaxValue)
        //    {
        //        throw new InvalidOperationException("Message envelope cannot be bigger than: " + ushort.MaxValue.ToString());
        //    }
        //    ushort oldpos = (ushort)(serialisationStream.Position - originalPos);//msglen +2 

        //    if (innerMsg == null)
        //    {
        //        serialisationStream.Position = originalPos;
        //        serialisationStream.Write(new byte[2], 0, 2);
        //        serialisationStream.Position = oldpos + originalPos;
        //        return;

        //    }

        //    var envLen = BitConverter.GetBytes(oldpos);
        //    serialisationStream.Position = originalPos;
        //    serialisationStream.Write(envLen, 0, 2);

        //    serialisationStream.Position = oldpos + originalPos;
        //    Serializer.Serialize(serialisationStream, innerMsg);
        //}

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //internal void SerializeMessage<E>(Stream serialisationStream, E envelope) where E : IMessageEnvelope
        //{
        //    // envelope+2, reserve for payload start index
        //    var originalPos = serialisationStream.Position;
        //    serialisationStream.Position = originalPos + 2;

        //    Serializer.Serialize(serialisationStream, envelope);
        //    if (serialisationStream.Position - originalPos >= ushort.MaxValue)
        //    {
        //        throw new InvalidOperationException("Message envelope cannot be bigger than: " + ushort.MaxValue.ToString());
        //    }
        //    ushort oldpos = (ushort)(serialisationStream.Position - originalPos);//msglen +2 

        //    if (envelope.PayloadCount == 0)
        //    {
        //        serialisationStream.Position = originalPos;
        //        serialisationStream.Write(new byte[2], 0, 2);
        //        serialisationStream.Position = oldpos + originalPos;
        //        return;
        //    }

        //    var envLen = BitConverter.GetBytes(oldpos);
        //    serialisationStream.Position = originalPos;
        //    serialisationStream.Write(envLen, 0, 2);

        //    serialisationStream.Position = oldpos + originalPos;
        //    serialisationStream.Write(envelope.Payload, envelope.PayloadOffset, envelope.PayloadCount);
        //}


    }
}
