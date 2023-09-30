using MessagePack;
using NetworkLibrary.Components;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessagePackNetwork.Components
{
    public class MessagepackSerializer : NetworkLibrary.MessageProtocol.ISerializer
    {
        public MessagepackSerializer()
        {

        }

        public T Deserialize<T>(Stream source)
        {
            var buff = ((PooledMemoryStream)source).GetBuffer();
            return MessagePackSerializer.Deserialize<T>(new ReadOnlyMemory<byte>(buff, 0, (int)source.Position));
        }

        public T Deserialize<T>(byte[] buffer, int offset, int count)
        {
            return MessagePackSerializer.Deserialize<T>(new ReadOnlyMemory<byte>(buffer, offset, count));
        }

        public void Serialize<T>(Stream destination, T instance)
        {
            var wrt = new BufferWriter((PooledMemoryStream)destination);
            MessagePackSerializer.Serialize(wrt, instance);
        }

        public byte[] Serialize<T>(T instance)
        {
            return MessagePackSerializer.Serialize(instance);
        }
    }

    internal readonly struct BufferWriter : IBufferWriter<byte>
    {
        public readonly PooledMemoryStream stream;

        public BufferWriter(PooledMemoryStream stream)
        {
            this.stream = stream;
        }

        public void Advance(int count)
        {
            stream.Position += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            stream.GetMemory(sizeHint, out var buffer, out int offset, out int count);
            return new Memory<byte>(buffer, offset, count);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            stream.GetMemory(sizeHint, out var buffer, out int offset, out int count);
            return new Span<byte>(buffer, offset, count);
        }
    }
}
