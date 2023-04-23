using NetworkLibrary.Components;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace MessagePackNetwork.Network.Components
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
            if (sizeHint < 256)
                sizeHint = 256;
            stream.Position += sizeHint;
            stream.Position -= sizeHint;
            var buffer = stream.GetBuffer();
            return new Memory<byte>(buffer, (int)stream.Position, sizeHint);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            if (sizeHint < 256)
                sizeHint = 256;
            stream.Position += sizeHint;
            stream.Position -= sizeHint;
            var buffer = stream.GetBuffer();
            return new Span<byte>(buffer, (int)stream.Position, sizeHint);
        }
    }
}
