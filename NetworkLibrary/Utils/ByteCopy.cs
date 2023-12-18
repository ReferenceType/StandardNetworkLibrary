using System;
using System.Runtime.CompilerServices;

namespace NetworkLibrary.Utils
{
    public class ByteCopy
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe byte[] ToArray(byte[] buffer, int offset, int count)
        {
            byte[] result = GetNewArray(count, false);

            fixed (byte* destination = result)
            {
                fixed (byte* message_ = &buffer[offset])
                    Buffer.MemoryCopy(message_, destination, count, count);
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] GetNewArray(int size = 65000, bool isPinned = false)
        {
#if NET5_0_OR_GREATER
            //Console.WriteLine("AADSADSA");
            return GC.AllocateUninitializedArray<byte>(size, pinned: isPinned);
#else
            return new byte[size];
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void BlockCopy(byte[] source, int sourceOffset, byte[] dest, int destinationOffset, int count)
        {
            //System.Buffer.BlockCopy(source, sourceOffset, dest, destinationOffset, count);
            fixed (byte* destination = &dest[destinationOffset])
            {
                fixed (byte* message_ = &source[sourceOffset])
                    Buffer.MemoryCopy(message_, destination, count, count);
            }
        }

       
        internal static byte[] Merge(byte[] b1, byte[] b2)
        {
            var bf=GetNewArray(b1.Length + b2.Length);
            BlockCopy(b1, 0, bf, 0, b1.Length);
            BlockCopy(b2, 0, bf, b1.Length, b2.Length);
            return bf;
        }
    }
}
