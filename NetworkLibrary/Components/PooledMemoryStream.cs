using System;
using System.Collections.Generic;
using System.Drawing;
using System.Dynamic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace NetworkLibrary.Components
{
    /*Tehre is no allccation here, all byte arrays comes from pool and retuned on flush */
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

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length =>  bufferInternal.LongLength;

        private long position = 0;

        public override long Position { get => position;
            set {
                if (bufferInternal.Length<value)
                    ExpandInternalBuffer((int)value);

                position = value;
            } }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Flush()
        {
            position= 0;
            if (bufferInternal.Length > 65537)
            {
                BufferPool.ReturnBuffer(bufferInternal);
                bufferInternal = BufferPool.RentBuffer(64000);
            }
           
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            if(Length<value)
                ExpandInternalBuffer((int)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] GetBuffer() => bufferInternal;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Write(byte[] buffer, int offset, int count)
        {
            if(bufferInternal.Length-position < count)
            {
                int demandSize = count + (bufferInternal.Length);
                if(demandSize> BufferPool.MaxBufferSize)
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
            position +=count;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExpandInternalBuffer(int size)
        {
            if(size <= bufferInternal.Length)
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
