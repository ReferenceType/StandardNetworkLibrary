using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkLibrary.Utils
{
    public class PrefixWriter
    {
        public static void WriteInt32AsBytes(ref byte[] buffer, int offset, int value)
        {
            buffer[0 + offset] = (byte)value;
            buffer[1 + offset] = (byte)(value >> 8);
            buffer[2 + offset] = (byte)(value >> 16);
            buffer[3 + offset] = (byte)(value >> 24);
        }

        public static void WriteInt32AsBytes(byte[] buffer, int offset, int value)
        {
            buffer[0 + offset] = (byte)value;
            buffer[1 + offset] = (byte)(value >> 8);
            buffer[2 + offset] = (byte)(value >> 16);
            buffer[3 + offset] = (byte)(value >> 24);
        }
    }
}
