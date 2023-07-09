using NetworkLibrary.Components;
using NetworkLibrary.Components.MessageBuffer;
using NetworkLibrary.MessageProtocol.Serialization;
using System;
using System.Runtime.CompilerServices;

namespace NetworkLibrary.MessageProtocol
{
    internal class GenericMessageQueue<S> : MessageBuffer, ISerialisableMessageQueue
         where S : ISerializer, new()
    {
        private readonly S Serializer = new S();
        public GenericMessageQueue(int maxIndexedMemory, bool writeLengthPrefix = true) : base(maxIndexedMemory, writeLengthPrefix)
        {

        }

        public bool TryEnqueueMessage<T>(MessageEnvelope envelope, T message)
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

        public bool TryEnqueueMessage(MessageEnvelope envelope, Action<PooledMemoryStream> serializationCallback)
        {
            lock (loki)
            {
                if (currentIndexedMemory < MaxIndexedMemory && !disposedValue)
                {
                    TotalMessageDispatched++;

                    // offset 4 for lenght prefix (reserve)
                    var originalPos = writeStream.Position32;
                    writeStream.Position32 += 4;

                    int msgLen = SerializeMessageWithInnerMessage(writeStream, envelope, serializationCallback);

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

        public bool TryEnqueueMessage(MessageEnvelope envelope)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int SerializeMessageWithInnerMessage<T>(PooledMemoryStream serialisationStream, MessageEnvelope empyEnvelope, T innerMsg)
        {
            // envelope+2
            int originalPos = serialisationStream.Position32;
            serialisationStream.Position32 += 2;

            EnvelopeSerializer.Serialize(serialisationStream, empyEnvelope);
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
        internal int SerializeMessageWithInnerMessage(PooledMemoryStream serialisationStream, MessageEnvelope empyEnvelope, Action<PooledMemoryStream> externalSerializationCallback)
        {
            // envelope+2
            int originalPos = serialisationStream.Position32;
            serialisationStream.Position32 += 2;

            EnvelopeSerializer.Serialize(serialisationStream, empyEnvelope);
            int delta = serialisationStream.Position32 - originalPos;

            if (delta >= ushort.MaxValue)
            {
                throw new InvalidOperationException("Message envelope cannot be bigger than: " + ushort.MaxValue.ToString());
            }
            ushort oldpos = (ushort)(delta);//msglen +2 

            if (externalSerializationCallback == null)
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

            externalSerializationCallback.Invoke(serialisationStream);

            return serialisationStream.Position32 - originalPos;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int SerializeMessage(PooledMemoryStream serialisationStream, MessageEnvelope envelope)
        {
            // envelope+2, reserve for payload start index
            int originalPos = serialisationStream.Position32;
            serialisationStream.Position32 += 2;

            EnvelopeSerializer.Serialize(serialisationStream, envelope);
            int delta = serialisationStream.Position32 - originalPos;

            if (delta >= ushort.MaxValue)
            {
                throw new InvalidOperationException("Message envelope cannot be bigger than: " + ushort.MaxValue.ToString());
            }
            ushort deltaPos = (ushort)(delta);//msglen +2 

            if (envelope.PayloadCount == 0)
            {
                var pos = serialisationStream.Position32;
                serialisationStream.Position32 = originalPos;
                serialisationStream.WriteTwoZerosUnchecked();
                serialisationStream.Position32 = pos;

                return delta;
            }
            var pos1 = serialisationStream.Position32;
            serialisationStream.Position32 = originalPos;
            serialisationStream.WriteUshortUnchecked(deltaPos);
            serialisationStream.Position32 = pos1;

            serialisationStream.Write(envelope.Payload, envelope.PayloadOffset, envelope.PayloadCount);

            return serialisationStream.Position32 - originalPos;
        }
    }
}
