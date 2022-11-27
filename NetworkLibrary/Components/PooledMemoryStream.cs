using System;
using System.Collections.Generic;
using System.Drawing;
using System.Dynamic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace NetworkLibrary.Components
{
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

        public override long Position { get => position; set => position = value; }

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
            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] GetBuffer() => bufferInternal;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Write(byte[] buffer, int offset, int count)
        {
            if(bufferInternal.Length-position < count)
            {
                if (count > bufferInternal.LongLength * 2)
                    ExpandInternalBuffer(count*2);
                else if(bufferInternal.Length != BufferPool.MaxBufferSize)
                    ExpandInternalBuffer(bufferInternal.Length*2);
                else
                    throw new InvalidOperationException("Cannot expand internal buffer to more than max amount");

            }
            //  Buffer.BlockCopy(buffer, offset, bufferInternal, (int)position, count);

            //try
            //{
            //    Buffer.BlockCopy(buffer, offset, bufferInternal, (int)position, count);

            //}
            //catch (Exception ex)
            //{ }
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
                //Buffer.BlockCopy(bufferInternal, 0, newBuf, 0, (int)position);
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
           
           // GC.SuppressFinalize(this);
            base.Dispose(disposing);
        }
    }
}
