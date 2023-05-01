using NetworkLibrary.Components;
using NetworkLibrary.Components.MessageBuffer;
using NetworkLibrary.MessageProtocol;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace NetworkLibrary.MessageProtocol

{
    public class GenericMessageQueue<S, E> : MessageBuffer, ISerialisableMessageQueue<E>
        where S : ISerializer, new()
        where E : IMessageEnvelope, new()
    {
        private readonly S Serializer = new S();
        public GenericMessageQueue(int maxIndexedMemory, bool writeLengthPrefix = true) : base(maxIndexedMemory, writeLengthPrefix)
        {

        }

        public bool TryEnqueueMessage<T>(E envelope, T message)
        {
            lock (loki)
            {
                if (currentIndexedMemory < MaxIndexedMemory && !disposedValue)
                {
                    TotalMessageDispatched++;

                    // offset 4 for lenght prefix (reserve)
                    var originalPos = writeStream.Position32;
                    writeStream.Position32 += 4;

                    int msgLen = SerializeMessageWithInnerMessage(writeStream, envelope, message);

                    var lastPos = writeStream.Position32;
                    writeStream.Position32 = originalPos;
                    writeStream.WriteIntUnchecked(msgLen);

                    writeStream.Position32 = lastPos;
                    currentIndexedMemory += msgLen + 4;

                    return true;

                }
            }
            return false;
        }

        public bool TryEnqueueMessage(E envelope)
        {

            lock (loki)
            {
                if (currentIndexedMemory < MaxIndexedMemory && !disposedValue)
                {
                    TotalMessageDispatched++;

                    // offset 4 for lenght prefix (reserve)
                    var originalPos = writeStream.Position32;
                    writeStream.Position32 += 4;
                    int msgLen = SerializeMessage(writeStream, envelope);

                    int lastPos = writeStream.Position32;
                    writeStream.Position32 = originalPos;
                    writeStream.WriteIntUnchecked(msgLen);

                    writeStream.Position32 = lastPos;
                    currentIndexedMemory += msgLen + 4;
                    return true;

                }
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int SerializeMessageWithInnerMessage<T>(PooledMemoryStream serialisationStream, E empyEnvelope, T innerMsg)
        {
            // envelope+2
            int originalPos = serialisationStream.Position32;
            serialisationStream.Position32 += 2;

            Serializer.Serialize(serialisationStream, empyEnvelope);
            int delta = serialisationStream.Position32 - originalPos;

            if (delta >= ushort.MaxValue)
            {
                throw new InvalidOperationException("Message envelope cannot be bigger than: " + ushort.MaxValue.ToString());
            }
            ushort oldpos = (ushort)(delta);//msglen +2 

            if (innerMsg == null)
            {
                var pos = serialisationStream.Position32;
                serialisationStream.Position32 = originalPos;
                serialisationStream.WriteTwoZerosUnchecked();
                serialisationStream.Position32 = pos;

                return delta;

            }

            var pos1 = serialisationStream.Position32;
            serialisationStream.Position32 = originalPos;
            serialisationStream.WriteUshortUnchecked(oldpos);
            serialisationStream.Position32 = pos1;

            Serializer.Serialize(serialisationStream, innerMsg);

            return serialisationStream.Position32 - originalPos;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int SerializeMessage(PooledMemoryStream serialisationStream, E envelope)
        {
            // envelope+2, reserve for payload start index
            int originalPos = serialisationStream.Position32;
            serialisationStream.Position32 += 2;

            Serializer.Serialize(serialisationStream, envelope);
            int delta = serialisationStream.Position32 - originalPos;

            if (delta >= ushort.MaxValue)
            {
                throw new InvalidOperationException("Message envelope cannot be bigger than: " + ushort.MaxValue.ToString());
            }

            ushort oldpos = (ushort)(delta);//msglen +2 

            if (envelope.PayloadCount == 0)
            {
                var pos = serialisationStream.Position32;
                serialisationStream.Position32 = originalPos;
                serialisationStream.WriteTwoZerosUnchecked();
                serialisationStream.Position32 = pos;
                return  delta;
            }

            var pos1 = serialisationStream.Position32;
            serialisationStream.Position32 = originalPos;
            serialisationStream.WriteUshortUnchecked(oldpos);
            serialisationStream.Position32 = pos1;

            serialisationStream.Write(envelope.Payload, envelope.PayloadOffset, envelope.PayloadCount);

            return serialisationStream.Position32 - originalPos;
        }
    }
}
