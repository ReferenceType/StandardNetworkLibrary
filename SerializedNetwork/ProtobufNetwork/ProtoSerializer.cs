using NetworkLibrary.Components;
using NetworkLibrary.MessageProtocol;
using NetworkLibrary.Utils;
using ProtoBuf;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace ProtobufNetwork
{
    public class ProtoSerializer : ISerializer
    {
        private ConcurrentObjectPool<PooledMemoryStream> streamPool = new ConcurrentObjectPool<PooledMemoryStream>();
        //private RuntimeTypeModel Serializer;

        public ProtoSerializer()
        {
            ProtoBuf.Serializer.PrepareSerializer<MessageEnvelope>();
            //ProtoBuf.Serializer.PrepareSerializer<IMessageEnvelope>();
            ProtoBuf.Serializer.PrepareSerializer<RouterHeader>();
            // Serializer = RuntimeTypeModel.Default;  
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Deserialize<T>(Stream source)
        {
            return Serializer.Deserialize<T>(source);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Deserialize<T>(byte[] buffer, int offset, int count)
        {
            //fixed (byte* startPointer = &buffer[offset])
            //{
            //    var span = new ReadOnlySpan<byte>(startPointer, count);
            //    return Serializer.Deserialize<T>(span);
            //}

            return Serializer.Deserialize<T>(new ReadOnlySpan<byte>(buffer, offset, count));
        }

        public MessageEnvelope Deserialize(byte[] buffer, int offset, int count)
        {
            return Serializer.Deserialize<MessageEnvelope>(new ReadOnlySpan<byte>(buffer, offset, count));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Serialize<T>(Stream destination, T instance)
        {
            Serializer.Serialize(destination, instance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] Serialize<T>(T instance)
        {
            var _stream = streamPool.RentObject();
            _stream.Position = 0;
            Serializer.Serialize(_stream, instance);

            var buffer = _stream.GetBuffer();
            var bytes = ByteCopy.ToArray(buffer, 0, (int)_stream.Position);

            _stream.Clear();
            streamPool.ReturnObject(_stream);
            return bytes;
        }

        public void Serialize(Stream destination, MessageEnvelope instance)
        {
            Serializer.Serialize<MessageEnvelope>(destination, instance);
        }
    }
}
