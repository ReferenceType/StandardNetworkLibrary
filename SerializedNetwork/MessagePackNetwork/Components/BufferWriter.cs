using NetworkLibrary.Components;
using System;
using System.Buffers;

namespace MessagePackNetwork.Components
{
    internal struct BufferWriter : IBufferWriter<byte>
    {
        public PooledMemoryStream stream;

        public BufferWriter(PooledMemoryStream stream)
        {
            this.stream = stream;
        }

        public void SetStream(PooledMemoryStream stream)
        {
            this.stream = stream;
        }

        public void Advance(int count)
        {
            stream.Position += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            //if (sizeHint < 256)
            //    sizeHint = 256;
            //stream.Position += sizeHint;
            //stream.Position -= sizeHint;
            //stream.Reserve(sizeHint);
            //var buffer = stream.GetBuffer();
            stream.GetMemory(sizeHint, out var buffer, out int offset, out int count);
            return new Memory<byte>(buffer, offset, count);
            //return new Memory<byte>(buffer, (int)stream.Position, sizeHint);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            //if (sizeHint < 256)
            //    sizeHint = 256;
            //stream.Position += sizeHint;
            //stream.Position -= sizeHint;
            //stream.Reserve(sizeHint);
            //var buffer = stream.GetBuffer();
            //return new Span<byte>(buffer, (int)stream.Position, sizeHint);

            stream.GetMemory(sizeHint, out var buffer, out int offset, out int count);
            return new Span<byte>(buffer, offset, count);
        }
    }
}
