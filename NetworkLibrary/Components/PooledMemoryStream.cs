using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Runtime.CompilerServices;

namespace NetworkLibrary.Components
{
    /*There is no allccation here, all byte arrays comes from pool and retuned on flush */
    public class PooledMemoryStream : Stream
    {
        byte[] bufferInternal;
        public PooledMemoryStream()
        {
            bufferInternal = BufferPool.RentBuffer(512);
        }

        public PooledMemoryStream(int minCapacity)
        {
            bufferInternal = BufferPool.RentBuffer(minCapacity);
        }
       
        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => true;

        public override long Length => bufferInternal.LongLength;

        private long position = 0;
        private int _origin = 0;

        public override long Position
        {
            get => position;
            set
            {
                if (bufferInternal.Length < value)
                    ExpandInternalBuffer((int)value);

                position = value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Flush()
        {
            position = 0;
            if (bufferInternal.Length > 65537)
            {
                BufferPool.ReturnBuffer(bufferInternal);
                bufferInternal = BufferPool.RentBuffer(64000);
            }

        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            count = count>Length-position? (int)Length - (int)position : count;
            unsafe
            {
                fixed (byte* destination = &buffer[offset])
                {
                    fixed (byte* toCopy = &bufferInternal[position])
                        Buffer.MemoryCopy(toCopy, destination, count, count);
                }
                Position += count;
                return count;
            }
           
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (offset > BufferPool.MaxBufferSize)
                throw new ArgumentOutOfRangeException("offset");
            switch (origin)
            {
                case SeekOrigin.Begin:
                    {
                        int tempPosition = unchecked(_origin + (int)offset);
                        if (offset < 0 || tempPosition < _origin)
                            throw new IOException("IO.IO_SeekBeforeBegin");
                        Position = tempPosition;
                        break;
                    }
                case SeekOrigin.Current:
                    {
                        int tempPosition = unchecked((int)position + (int)offset);
                        if (unchecked(position + offset) < _origin || tempPosition < _origin)
                            throw new IOException("IO.IO_SeekBeforeBegin");
                        Position = tempPosition;
                        break;
                    }
                case SeekOrigin.End:
                    {
                        int tempPosition = unchecked((int)position + (int)offset);
                        if (unchecked(position + offset) < _origin || tempPosition < _origin)
                            throw new IOException("IO.IO_SeekBeforeBegin");
                        Position = tempPosition;
                        break;
                    }
                default:
                    throw new ArgumentException("Argument_InvalidSeekOrigin");
            }

           
            return position;

        }

        public override void SetLength(long value)
        {
            if (Length < value)
                ExpandInternalBuffer((int)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] GetBuffer() => bufferInternal;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (bufferInternal.Length - position < count)
            {
                int demandSize = count + (bufferInternal.Length);
                if (demandSize > BufferPool.MaxBufferSize)
                    throw new InvalidOperationException("Cannot expand internal buffer to more than max amount");
                else
                    ExpandInternalBuffer(demandSize);// this at least doubles the buffer 
            }
            // fastest copy
            unsafe
            {
                fixed (byte* destination = &bufferInternal[position])
                {
                    fixed (byte* toCopy = &buffer[offset])
                        Buffer.MemoryCopy(toCopy, destination, count, count);
                }
            }
            position += count;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExpandInternalBuffer(int size)
        {
            if (size <= bufferInternal.Length)
                throw new InvalidOperationException("Cannot expand internal buffer to smaller size");


            var newBuf = BufferPool.RentBuffer(size);
            if (position > 0)
            {
                unsafe
                {
                    fixed (byte* destination = newBuf)
                    {
                        fixed (byte* toCopy = bufferInternal)
                            Buffer.MemoryCopy(toCopy, destination, position, position);
                    }
                }
            }

            BufferPool.ReturnBuffer(bufferInternal);
            bufferInternal = newBuf;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                BufferPool.ReturnBuffer(bufferInternal);
                bufferInternal = null;
            }
            base.Dispose(disposing);
        }
    }
}
