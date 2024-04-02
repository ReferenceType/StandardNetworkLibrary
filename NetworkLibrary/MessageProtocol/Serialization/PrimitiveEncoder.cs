using NetworkLibrary.Components;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace NetworkLibrary
{
    public class PrimitiveEncoder
    {
        #region Bool
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteBool(PooledMemoryStream stream, bool value)
        {
            if (value)
                stream.WriteByte(1);
            else
                stream.WriteByte(0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ReadBool(PooledMemoryStream stream)
        {
            return (byte)stream.ReadByte() == 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ReadBool(byte[] buffer, ref int offs)
        {
            return buffer[offs++] == 1;
        }
        #endregion

        #region Byte
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteByte(PooledMemoryStream stream, byte value)
        {
            stream.WriteByte(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ReadByte(PooledMemoryStream stream)
        {
            return (byte)stream.ReadByte();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ReadByte(byte[] buffer, ref int offs)
        {
            return buffer[offs++];
        }
        #endregion

        #region Char

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteChar(PooledMemoryStream stream, char value)
        {
            WriteUInt32(stream, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static char ReadChar(PooledMemoryStream stream)
        {
            return (char)ReadUInt32(stream);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static char ReadChar(byte[] buffer, ref int offs)
        {
            return (char)ReadUInt32(buffer, ref offs);
        }
        #endregion

        #region Short
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteInt16(PooledMemoryStream stream, short value)
        {
            var encoded = (uint)(value << 1 ^ value >> 31);

            for (; encoded >= 0x80u; encoded >>= 7)
                stream.WriteByte((byte)(encoded | 0x80u));

            stream.WriteByte((byte)encoded);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ReadInt16(PooledMemoryStream stream)
        {
            int result = 0;
            int offset = 0;

            for (; offset < 32; offset += 7)
            {
                int b = stream.ReadByte();
                if (b == -1)
                    throw new EndOfStreamException();

                result |= (b & 0x7f) << offset;

                if ((b & 0x80) == 0)
                    return (short)((int)((uint)result >> 1) ^ -(int)((uint)result & 1));

            }
            throw new InvalidDataException();

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ReadInt16(byte[] buffer, ref int offs)
        {
            int result = 0;
            int offset = 0;

            for (; offset < 32; offset += 7)
            {
                int b = buffer[offs++];

                result |= (b & 0x7f) << offset;

                if ((b & 0x80) == 0)
                    return (short)((int)((uint)result >> 1) ^ -(int)((uint)result & 1));

            }
            throw new InvalidDataException();

        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUint16(PooledMemoryStream stream, ushort value)
        {
            for (; value >= 0x80u; value >>= 7)
                stream.WriteByte((byte)(value | 0x80u));

            stream.WriteByte((byte)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteFixedUint16(PooledMemoryStream stream, ushort value)
        {
            stream.WriteUshort(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void WriteFixedUint16(byte[]buffer, int offset, ushort value)
        {
            if(buffer.Length-offset < 2)
                throw new InvalidDataException("Buffer does not have enough space");

            fixed (byte* b = &buffer[offset])
                *(short*)b = (short)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUInt16(PooledMemoryStream stream)
        {
            int result = 0;
            int offset = 0;

            for (; offset < 32; offset += 7)
            {
                int b = stream.ReadByte();
                if (b == -1)
                    throw new EndOfStreamException();

                result |= (b & 0x7f) << offset;

                if ((b & 0x80) == 0)
                    return (ushort)result;

            }
            throw new InvalidDataException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUInt16(byte[] buffer, ref int offs)
        {
            int result = 0;
            int offset = 0;

            for (; offset < 32; offset += 7)
            {
                int b = buffer[offs++];


                result |= (b & 0x7f) << offset;

                if ((b & 0x80) == 0)
                    return (ushort)result;

            }
            throw new InvalidDataException();
        }
        #endregion

        #region Int

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteInt32(PooledMemoryStream stream, int value)
        {
            var encoded = (uint)(value << 1 ^ value >> 31);

            for (; encoded >= 0x80u; encoded >>= 7)
                stream.WriteByte((byte)(encoded | 0x80u));

            stream.WriteByte((byte)encoded);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteInt32(byte[] buffer, ref int offset, int value)
        {
            var encoded = (uint)(value << 1 ^ value >> 31);

            for (; encoded >= 0x80u; encoded >>= 7)
                buffer[offset++] = ((byte)(encoded | 0x80u));

            buffer[offset++] = ((byte)encoded);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void WriteInt32(byte* buffer, ref int offset, int value)
        {
            var encoded = (uint)(value << 1 ^ value >> 31);

            for (; encoded >= 0x80u; encoded >>= 7)
                buffer[offset++] = ((byte)(encoded | 0x80u));

            buffer[offset++] = ((byte)encoded);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt32(PooledMemoryStream stream)
        {
            int result = 0;
            int offset = 0;

            for (; offset < 32; offset += 7)
            {
                int b = stream.ReadByte();
                if (b == -1)
                    throw new EndOfStreamException();

                result |= (b & 0x7f) << offset;

                if ((b & 0x80) == 0)
                    return (int)((uint)result >> 1) ^ -(int)((uint)result & 1);

            }
            throw new InvalidDataException();

        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt32(byte[] buffer, ref int offs)
        {
            int result = 0;
            int offset = 0;

            for (; offset < 32; offset += 7)
            {
                int b = buffer[offs++];

                result |= (b & 0x7f) << offset;

                if ((b & 0x80) == 0)
                    return (int)((uint)result >> 1) ^ -(int)((uint)result & 1);

            }
            throw new InvalidDataException();

        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUInt32(PooledMemoryStream stream, uint value)
        {
            for (; value >= 0x80u; value >>= 7)
                stream.WriteByte((byte)(value | 0x80u));

            stream.WriteByte((byte)value);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUInt32(byte[] buffer,ref int offset, uint value)
        {
            for (; value >= 0x80u; value >>= 7)
               buffer[offset++]= ((byte)(value | 0x80u));

            buffer[offset++] = ((byte)value);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void WriteUInt32(byte* buffer, ref int offset, uint value)
        {
            for (; value >= 0x80u; value >>= 7)
                buffer[offset++] = ((byte)(value | 0x80u));

            buffer[offset++] = ((byte)value);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadUInt32(PooledMemoryStream stream)
        {
            int result = 0;
            int offset = 0;

            for (; offset < 32; offset += 7)
            {
                int b = stream.ReadByte();
                if (b == -1)
                    throw new EndOfStreamException();

                result |= (b & 0x7f) << offset;

                if ((b & 0x80) == 0)
                    return (uint)result;

            }
            throw new InvalidDataException();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadUInt32(byte[] buffer, ref int offs)
        {
            int result = 0;
            int offset = 0;

            for (; offset < 32; offset += 7)
            {
                int b = buffer[offs++];


                result |= (b & 0x7f) << offset;

                if ((b & 0x80) == 0)
                    return (uint)result;

            }
            throw new InvalidDataException();
        }

        #endregion

        #region Long

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteInt64(PooledMemoryStream stream, long value)
        {
            var encoded = (ulong)(value << 1 ^ value >> 63);

            for (; encoded >= 0x80u; encoded >>= 7)
                stream.WriteByte((byte)(encoded | 0x80u));

            stream.WriteByte((byte)encoded);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteInt64(byte[] buffer, ref int offset, long value)
        {
            var encoded = (ulong)(value << 1 ^ value >> 63);

            for (; encoded >= 0x80u; encoded >>= 7)
                buffer[offset++] = ((byte)(encoded | 0x80u));

            buffer[offset++] = ((byte)encoded);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ReadInt64(PooledMemoryStream stream)
        {
            long result = 0;
            int offset = 0;

            for (; offset < 64; offset += 7)
            {
                int b = stream.ReadByte();
                if (b == -1)
                    throw new EndOfStreamException();

                result |= (long)(b & 0x7f) << offset;

                if ((b & 0x80) == 0)
                {
                    return (long)((ulong)result >> 1) ^ -(long)((ulong)result & 1);
                }
            }

            throw new InvalidDataException();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ReadInt64(byte[] buffer, ref int offs)
        {
            long result = 0;
            int offset = 0;

            for (; offset < 64; offset += 7)
            {
                int b = buffer[offs++];


                result |= (long)(b & 0x7f) << offset;

                if ((b & 0x80) == 0)
                {
                    return (long)((ulong)result >> 1) ^ -(long)((ulong)result & 1);
                }
            }

            throw new InvalidDataException();
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUInt64(PooledMemoryStream stream, ulong value)
        {

            for (; value >= 0x80u; value >>= 7)
                stream.WriteByte((byte)(value | 0x80u));

            stream.WriteByte((byte)value);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ReadUInt64(PooledMemoryStream stream)
        {
            long result = 0;
            int offset = 0;

            for (; offset < 64; offset += 7)
            {
                int b = stream.ReadByte();
                if (b == -1)
                    throw new EndOfStreamException();

                result |= (long)(b & 0x7f) << offset;

                if ((b & 0x80) == 0)
                {
                    return (ulong)result;
                }
            }

            throw new InvalidDataException();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ReadUInt64(byte[] buffer, ref int offs)
        {
            long result = 0;
            int offset = 0;

            for (; offset < 64; offset += 7)
            {
                int b = buffer[offs++];
                if (b == -1)
                    throw new EndOfStreamException();

                result |= (long)(b & 0x7f) << offset;

                if ((b & 0x80) == 0)
                {
                    return (ulong)result;
                }
            }

            throw new InvalidDataException();
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void WriteFixedInt64(PooledMemoryStream stream, long value)
        {
            stream.Reserve(8);
            var buffer = stream.GetBuffer();
            int pos = stream.Position32;
            stream.Advance(8);

            fixed (byte* b = &buffer[pos])
                *(long*)b = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void WriteFixedInt64(byte[] buffer,ref int offset, long value)
        {           
            fixed (byte* b = &buffer[offset])
                *(long*)b = value;
            offset += 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void WriteFixedInt64(byte* buffer, ref int offset, long value)
        {
            byte* b = buffer + offset;
                *(long*)b = value;
            offset += 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe long ReadFixedInt64(PooledMemoryStream stream)
        {
            var buffer = stream.GetBuffer();
            int offset = stream.Position32;
            stream.Advance(8);


            fixed (byte* pbyte = &buffer[offset])
                return *(long*)pbyte;

        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe long ReadFixedInt64(byte[] buffer, ref int offs)
        {
            fixed (byte* pbyte = &buffer[offs])
            {
                offs += 8;
                return *(long*)pbyte;
            }
        }
        #endregion

        #region Float

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void WriteFloat(PooledMemoryStream stream, float value)
        {
            uint v = *(uint*)&value;
            WriteUInt32(stream, v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe float ReadFloat(PooledMemoryStream stream)
        {
            uint v = ReadUInt32(stream);
            var value = *(float*)&v;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe float ReadFloat(byte[] buffer, ref int offs)
        {
            uint v = ReadUInt32(buffer, ref offs);
            var value = *(float*)&v;
            return value;
        }
        #endregion

        #region Double
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void WriteDouble(PooledMemoryStream stream, double value)
        {
            ulong v = *(ulong*)&value;
            WriteUInt64(stream, v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe double ReadDouble(PooledMemoryStream stream)
        {
            ulong v = ReadUInt64(stream);
            var value = *(double*)&v;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe double ReadDouble(byte[] buffer, ref int offs)
        {
            ulong v = ReadUInt64(buffer, ref offs);
            var value = *(double*)&v;
            return value;
        }
        #endregion

        #region Decimal

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteDecimal(PooledMemoryStream stream, decimal value)
        {
            int[] bits = decimal.GetBits(value);

            ulong low = (uint)bits[0];
            ulong mid = (ulong)(uint)bits[1] << 32;
            ulong lowmid = low | mid;

            uint high = (uint)bits[2];

            uint scale = (uint)bits[3] >> 15 & 0x01fe;
            uint sign = (uint)bits[3] >> 31;
            uint scaleSign = scale | sign;

            WriteUInt64(stream, lowmid);
            WriteUInt32(stream, high);
            WriteUInt32(stream, scaleSign);

        }

        [ThreadStatic]
        static int[] s_decimalBitsArray;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal ReadDecimal(PooledMemoryStream stream)
        {
            ulong lowmid;
            uint high, scaleSign;

            lowmid = ReadUInt64(stream);
            high = ReadUInt32(stream);
            scaleSign = ReadUInt32(stream);

            int scale = (int)((scaleSign & ~1) << 15);
            int sign = (int)((scaleSign & 1) << 31);

            var arr = s_decimalBitsArray;
            if (arr == null)
                arr = s_decimalBitsArray = new int[4];

            arr[0] = (int)lowmid;
            arr[1] = (int)(lowmid >> 32);
            arr[2] = (int)high;
            arr[3] = scale | sign;

            return new decimal(arr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal ReadDecimal(byte[] buffer, ref int offs)
        {
            ulong lowmid;
            uint high, scaleSign;

            lowmid = ReadUInt64(buffer, ref offs);
            high = ReadUInt32(buffer, ref offs);
            scaleSign = ReadUInt32(buffer, ref offs);

            int scale = (int)((scaleSign & ~1) << 15);
            int sign = (int)((scaleSign & 1) << 31);

            var arr = s_decimalBitsArray;
            if (arr == null)
                arr = s_decimalBitsArray = new int[4];

            arr[0] = (int)lowmid;
            arr[1] = (int)(lowmid >> 32);
            arr[2] = (int)high;
            arr[3] = scale | sign;

            return new decimal(arr);
        }

        #endregion

        #region Guid



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void WriteGuid(PooledMemoryStream stream, Guid value)
        {
            //long* ptr = (long*)&value;
            //{
            //    WriteInt64(stream, *ptr++);
            //    WriteInt64(stream, *ptr);
            //}
            stream.Reserve(16);
            var buffer = stream.GetBuffer();
            int pos = stream.Position32;

            long* ptr = (long*)&value;
            {
                fixed (byte* b = &buffer[pos])
                    *(long*)b = *ptr++;

                fixed (byte* b = &buffer[pos + 8])
                    *(long*)b = *ptr;
            }
            stream.Advance(16);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void WriteGuid(byte[] buffer, ref int offset, Guid value)
        {
           
            int pos = offset;

            long* ptr = (long*)&value;
            {
                fixed (byte* b = &buffer[pos])
                    *(long*)b = *ptr++;

                fixed (byte* b = &buffer[pos + 8])
                    *(long*)b = *ptr;
            }
            offset+= 16;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void WriteGuid(byte* buffer, ref int offset, Guid value)
        {
            int pos = offset;

            long* ptr = (long*)&value;
            {
                byte* b = buffer + pos;
                    *(long*)b = *ptr++;

                byte* bb = buffer + (pos + 8);
                    *(long*)bb = *ptr;
            }
            offset += 16;
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref Guid ReadGuid(PooledMemoryStream stream)
        {
            //long* ptr = stackalloc long[2];
            //ptr[0] = ReadInt64(stream);
            //ptr[1] = ReadInt64(stream);
            //return ref *(Guid*)ptr;


            var buffer = stream.GetBuffer();
            int offset = stream.Position32;

            long* ptr = stackalloc long[2];
            fixed (byte* pbyte = &buffer[offset])
                ptr[0] = *(long*)pbyte;
            fixed (byte* pbyte = &buffer[offset + 8])
                ptr[1] = *(long*)pbyte;

            stream.Advance(16);
            return ref *(Guid*)ptr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref Guid ReadGuid(byte[] buffer, ref int offset)
        {
            //long* ptr = stackalloc long[2];
            //ptr[0] = ReadInt64(buffer, ref offset);
            //ptr[1] = ReadInt64(buffer, ref offset);
            //return ref *(Guid*)ptr;


            long* ptr = stackalloc long[2];
            fixed (byte* pbyte = &buffer[offset])
                ptr[0] = *(long*)pbyte;
            fixed (byte* pbyte = &buffer[offset + 8])
                ptr[1] = *(long*)pbyte;
            offset += 16;
            return ref *(Guid*)ptr;
        }

        #endregion

        #region DateTime
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteDatetime(PooledMemoryStream stream, DateTime val)
        {
            var value = val.ToBinary();
            var encoded = (ulong)(value << 1 ^ value >> 63);

            for (; encoded >= 0x80u; encoded >>= 7)
                stream.WriteByte((byte)(encoded | 0x80u));

            stream.WriteByte((byte)encoded);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DateTime ReadDatetime(PooledMemoryStream stream)
        {
            long result = 0;
            int offset = 0;

            for (; offset < 64; offset += 7)
            {
                int b = stream.ReadByte();
                if (b == -1)
                    throw new EndOfStreamException();

                result |= (long)(b & 0x7f) << offset;

                if ((b & 0x80) == 0)
                {
                    return DateTime.FromBinary((long)((ulong)result >> 1) ^ -(long)((ulong)result & 1));
                }
            }

            throw new InvalidDataException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe DateTime ReadDatetime(byte[] buffer, ref int offs)
        {
            long result = 0;
            int offset = 0;

            for (; offset < 64; offset += 7)
            {
                int b = buffer[offs++];


                result |= (long)(b & 0x7f) << offset;

                if ((b & 0x80) == 0)
                {
                    return DateTime.FromBinary((long)((ulong)result >> 1) ^ -(long)((ulong)result & 1));
                }
            }

            throw new InvalidDataException();
        }
        #endregion

        #region String

        static ASCIIEncoding encoder = new ASCIIEncoding();
        [ThreadStatic]
        static UTF8Encoding encoderUtf8 = new UTF8Encoding(false, true);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void WriteStringUtf8(PooledMemoryStream stream, string value)
        {
            if (value == null)
            {
                stream.WriteByte(0);
                return;
            }

            if (value.Length == 0)
            {
                stream.WriteByte(1);
                stream.WriteByte(0);
                return;
            }

            if (encoderUtf8 == null) encoderUtf8 = new UTF8Encoding(false, true);
            //int len = encoderUtf8.GetMaxByteCount(value.Length);
            int len = encoderUtf8.GetByteCount(value);
            // int len = value.Length*4;
            stream.Reserve(len + 4);


            WriteUInt32(stream, (uint)len);

            var buf = stream.GetBuffer();
            fixed (char* src = value)
            fixed (byte* dst = &buf[stream.Position32])
            {
                stream.Advance(encoderUtf8.GetBytes(src, value.Length, dst, len));
            }

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void WriteStringUtf8(byte[] buffer,ref int offset, string value)
        {
            if (value == null)
            {
                buffer[offset++] = 0;
                return;
            }

            if (value.Length == 0)
            {
                buffer[offset++] = 1;
                buffer[offset++] = 0;
                return;
            }

            if (encoderUtf8 == null) encoderUtf8 = new UTF8Encoding(false, true);
            //int len = encoderUtf8.GetMaxByteCount(value.Length);
            int len = encoderUtf8.GetByteCount(value);
            // int len = value.Length*4;
           


            WriteUInt32(buffer,ref offset, (uint)len);

            var buf = buffer;
            fixed (char* src = value)
            fixed (byte* dst = &buffer[offset])
            {
                offset += encoderUtf8.GetBytes(src, value.Length, dst, len);
            }

        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void WriteStringUtf8(byte* buffer, ref int offset, string value)
        {
            if (value == null)
            {
                buffer[offset++] = 0;
                return;
            }

            if (value.Length == 0)
            {
                buffer[offset++] = 1;
                buffer[offset++] = 0;
                return;
            }

            if (encoderUtf8 == null) encoderUtf8 = new UTF8Encoding(false, true);
            //int len = encoderUtf8.GetMaxByteCount(value.Length);
            int len = encoderUtf8.GetByteCount(value);
            // int len = value.Length*4;



            WriteUInt32(buffer, ref offset, (uint)len);

            var buf = buffer;
            fixed (char* src = value)
            {
                byte* dst = buffer + offset;
                offset += encoderUtf8.GetBytes(src, value.Length, dst, len);

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe string ReadStringUtf8(PooledMemoryStream stream)
        {
            if (encoderUtf8 == null) encoderUtf8 = new UTF8Encoding(false, true);

            var len = ReadUInt32(stream);
            if (len == 0)
                return null;

            if (len == 1)
            {
                byte val = (byte)stream.ReadByte();
                if (val == 0)
                    return "";
                else
                {
                    return encoderUtf8.GetString(&val, 1);
                }
            }

            var buff = stream.GetBuffer();
            int pos = stream.Position32;
            stream.Position += len;
            fixed (byte* start = &buff[pos])
            {
                return encoderUtf8.GetString(start, (int)len);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe string ReadStringUtf8(byte[] buffer, ref int offs)
        {
            if (encoderUtf8 == null) encoderUtf8 = new UTF8Encoding(false, true);

            var len = ReadUInt32(buffer, ref offs);
            if (len == 0)
                return null;

            if (len == 1)
            {
                byte val = buffer[offs++];
                if (val == 0)
                    return "";
                else
                {
                    return encoderUtf8.GetString(&val, 1);
                }
            }


            fixed (byte* start = &buffer[offs])
            {
                offs += (int)len;
                return encoderUtf8.GetString(start, (int)len);
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void WriteStringASCII(PooledMemoryStream stream, string value)
        {
            if (value == null)
            {
                stream.WriteByte(0);
                return;
            }

            if (value.Length == 0)
            {
                stream.WriteByte(1);
                stream.WriteByte(0);
                return;
            }

            int len = value.Length;
            stream.Reserve(len + 4);

            WriteUInt32(stream, (uint)len);

            var buf = stream.GetBuffer();
            fixed (char* src = value)
            fixed (byte* dst = &buf[stream.Position32])
            {
                encoder.GetBytes(src, len, dst, len);
            }

            stream.Advance(len);

        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void WriteStringASCII2(PooledMemoryStream stream, string value)
        {
            if (value == null)
            {
                stream.WriteByte(0);
                return;
            }

            if (value.Length == 0)
            {
                stream.WriteByte(1);
                stream.WriteByte(0);
                return;
            }

            int len = value.Length;
            stream.Reserve(len + 4);

            WriteUInt32(stream, (uint)len);

            var buffer = stream.GetBuffer();
            int pos = stream.Position32;
            for (int i = 0; i < len; i++)
            {
                buffer[i + pos] = (byte)(value[i] & 0x7f);
            }

            stream.Advance(len);

        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe string ReadStringASCII(PooledMemoryStream stream)
        {
            var len = ReadUInt32(stream);
            if (len == 0)
                return null;

            if (len == 1)
            {
                byte val = (byte)stream.ReadByte();
                if (val == 0)
                    return "";
                else
                {
                    return encoder.GetString(&val, 1);
                }
            }

            var buff = stream.GetBuffer();
            fixed (byte* start = &buff[stream.Position32])
            {
                stream.Advance((int)len);
                return encoder.GetString(start, (int)len);
            }

        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe string ReadStringASCII(byte[] buffer, ref int offs)
        {
            var len = ReadUInt32(buffer, ref offs);
            if (len == 0)
                return null;

            if (len == 1)
            {
                byte val = buffer[offs++];
                if (val == 0)
                    return "";
                else
                {
                    return encoder.GetString(&val, 1);
                }
            }


            fixed (byte* start = &buffer[offs])
            {
                offs += (int)len;
                return encoder.GetString(start, (int)len);
            }
        }


        #endregion

    }

}



