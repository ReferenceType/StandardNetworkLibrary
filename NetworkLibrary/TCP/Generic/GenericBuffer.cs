using NetworkLibrary.Components.MessageBuffer;
using NetworkLibrary.MessageProtocol;
using System;
namespace NetworkLibrary.TCP.Generic
{
    internal class GenericBuffer<S> : MessageBuffer where S : ISerializer, new()
    {
        private readonly S serializer = new S();
        public GenericBuffer(int maxIndexedMemory, bool writeLengthPrefix = true) : base(maxIndexedMemory, writeLengthPrefix)
        {
        }

        public bool TryEnqueueMessage<T>(T instance)
        {

            lock (loki)
            {
                if (currentIndexedMemory < MaxIndexedMemory && !disposedValue)
                {
                    TotalMessageDispatched++;

                    if (writeLengthPrefix)
                    {
                        // offset 4 for lenght prefix (reserve)
                        writeStream.Position32 += 4;
                    }

                    int initalPos = writeStream.Position32;
                    serializer.Serialize(writeStream, instance);
                    int msgLen = writeStream.Position32 - initalPos;

                    if (writeLengthPrefix)
                    {
                        var lastPos = writeStream.Position32;

                        writeStream.Position32 = initalPos - 4;
                        writeStream.WriteIntUnchecked(msgLen);
                        writeStream.Position32 = lastPos;

                        // for the currentIndexedMemory
                        msgLen += 4;
                    }


                    currentIndexedMemory += msgLen;
                    return true;

                }
            }
            return false;
        }
    }
}
