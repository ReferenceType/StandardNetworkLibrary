using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace NetworkLibrary.Utils
{
    public class ByteCopy
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe byte[] ToArray(byte[] buffer, int offset,int count)
        {
            byte[] result = new byte[count];
            fixed (byte* destination = result)
            {
                fixed (byte* message_ = &buffer[offset])
                    Buffer.MemoryCopy(message_, destination, count,count);
            }
            return result;
        }

        public static byte[] ToArray2(byte[] buffer, int offset, int count)
        {
            byte[] result = new byte[count];
            Buffer.BlockCopy(buffer, offset, result, 0, count);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void BlockCopy(byte[] source,int sourceOffset, byte[] dest, int destinationOffset, int count)
        {
            //System.Buffer.BlockCopy(message, 0, bufferInternal, offset, message.Length);
            fixed (byte* destination = &dest[destinationOffset])
            {
                fixed (byte* message_ = &source[sourceOffset])
                    Buffer.MemoryCopy(message_, destination, count, count);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteInt32(byte[] bufferInternal, int offset,int value)
        {
            if (BitConverter.IsLittleEndian)
            {
                bufferInternal[offset] = (byte)value;
                bufferInternal[offset + 1] = (byte)(value >> 8);
                bufferInternal[offset + 2] = (byte)(value >> 16);
                bufferInternal[offset + 3] = (byte)(value >> 24);
            }
            else
            {
                bufferInternal[offset + 3] = (byte)value;
                bufferInternal[offset + 2] = (byte)(value >> 8);
                bufferInternal[offset + 1] = (byte)(value >> 16);
                bufferInternal[offset] = (byte)(value >> 24);
            }
        }
    }
}
