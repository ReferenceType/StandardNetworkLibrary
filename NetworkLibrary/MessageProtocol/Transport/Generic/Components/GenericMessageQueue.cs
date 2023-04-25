using NetworkLibrary.Components;
using NetworkLibrary.Components.MessageBuffer;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace MessageProtocol
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
                if (Volatile.Read(ref currentIndexedMemory) < MaxIndexedMemory && !disposedValue)
                {
                    TotalMessageDispatched++;

                    // offset 4 for lenght prefix (reserve)
                    var originalPos = writeStream.Position;
                    writeStream.Position += 4;

                     int msgLen = SerializeMessageWithInnerMessage(writeStream, envelope, message);

                    //int msgLen = (int)(writeStream.Position - (originalPos + 4));
                    var msgLenBytes = BitConverter.GetBytes(msgLen);

                    // write the message lenght on reserved field
                    var lastPos = writeStream.Position;
                    writeStream.Position = originalPos;
                    writeStream.Write(msgLenBytes, 0, 4);
                    writeStream.Position = lastPos;
                    Interlocked.Add(ref currentIndexedMemory, msgLen + 4);

                    return true;

                }
            }
            return false;
        }

        public bool TryEnqueueMessage(E envelope)
        {

            lock (loki)
            {
                if (Volatile.Read(ref currentIndexedMemory) < MaxIndexedMemory && !disposedValue)
                {
                    TotalMessageDispatched++;

                    var originalPos = writeStream.Position;
                    writeStream.Position += 4;
                    int msgLen = SerializeMessage(writeStream, envelope);

                   // int msgLen = (int)(writeStream.Position - (originalPos + 4));
                    var msgLenBytes = BitConverter.GetBytes(msgLen);

                    var lastPos = writeStream.Position;
                    writeStream.Position = originalPos;
                    writeStream.Write(msgLenBytes, 0, 4);

                    writeStream.Position = lastPos;
                    Interlocked.Add(ref currentIndexedMemory, msgLen + 4);

                    return true;

                }
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int SerializeMessageWithInnerMessage<T>(PooledMemoryStream serialisationStream, E empyEnvelope, T innerMsg)
        {
            // envelope+2
            var originalPos = serialisationStream.Position;

            serialisationStream.Position = originalPos + 2;
            Serializer.Serialize(serialisationStream, empyEnvelope);

            if (serialisationStream.Position - originalPos >= ushort.MaxValue)
            {
                throw new InvalidOperationException("Message envelope cannot be bigger than: " + ushort.MaxValue.ToString());
            }
            ushort oldpos = (ushort)(serialisationStream.Position - originalPos);//msglen +2 

            if (innerMsg == null)
            {
                serialisationStream.Position = originalPos;
                serialisationStream.Write(new byte[2], 0, 2);
                serialisationStream.Position = oldpos + originalPos;
                return (int)serialisationStream.Position - (int)originalPos;

            }

            var envLen = BitConverter.GetBytes(oldpos);
            serialisationStream.Position = originalPos;
            serialisationStream.Write(envLen, 0, 2);

            serialisationStream.Position = oldpos + originalPos;
            Serializer.Serialize(serialisationStream, innerMsg);

            return (int)serialisationStream.Position - (int)originalPos;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int SerializeMessage(PooledMemoryStream serialisationStream, E envelope)
        {
            // envelope+2, reserve for payload start index
            var originalPos = serialisationStream.Position;
            serialisationStream.Position = originalPos + 2;

            Serializer.Serialize(serialisationStream, envelope);
            if (serialisationStream.Position - originalPos >= ushort.MaxValue)
            {
                throw new InvalidOperationException("Message envelope cannot be bigger than: " + ushort.MaxValue.ToString());
            }
            ushort oldpos = (ushort)(serialisationStream.Position - originalPos);//msglen +2 

            if (envelope.PayloadCount == 0)
            {
                serialisationStream.Position = originalPos;
                serialisationStream.Write(new byte[2], 0, 2);
                serialisationStream.Position = oldpos + originalPos;
                return (int)serialisationStream.Position - (int)originalPos; ;
            }

            var envLen = BitConverter.GetBytes(oldpos);
            serialisationStream.Position = originalPos;
            serialisationStream.Write(envLen, 0, 2);

            serialisationStream.Position = oldpos + originalPos;
            serialisationStream.Write(envelope.Payload, envelope.PayloadOffset, envelope.PayloadCount);

            return (int)serialisationStream.Position - (int)originalPos; ;
        }
    }
}
