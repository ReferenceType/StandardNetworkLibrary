using System;
using System.Runtime.CompilerServices;

namespace NetworkLibrary.Utils
{
    public class ByteCopy
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe byte[] ToArray(byte[] buffer, int offset, int count)
        {
            byte[] result = new byte[count];
            fixed (byte* destination = result)
            {
                fixed (byte* message_ = &buffer[offset])
                    Buffer.MemoryCopy(message_, destination, count, count);
            }
            return result;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void BlockCopy(byte[] source, int sourceOffset, byte[] dest, int destinationOffset, int count)
        {
            //System.Buffer.BlockCopy(message, 0, bufferInternal, offset, message.Length);
            fixed (byte* destination = &dest[destinationOffset])
            {
                fixed (byte* message_ = &source[sourceOffset])
                    Buffer.MemoryCopy(message_, destination, count, count);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteInt32(byte[] buffer, int offset, int value)
        {
            if (BitConverter.IsLittleEndian)
            {
                buffer[offset] = (byte)value;
                buffer[offset + 1] = (byte)(value >> 8);
                buffer[offset + 2] = (byte)(value >> 16);
                buffer[offset + 3] = (byte)(value >> 24);
            }
            else
            {
                buffer[offset + 3] = (byte)value;
                buffer[offset + 2] = (byte)(value >> 8);
                buffer[offset + 1] = (byte)(value >> 16);
                buffer[offset] = (byte)(value >> 24);
            }
        }
    }
}
