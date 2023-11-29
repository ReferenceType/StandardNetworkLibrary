using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetworkLibrary.Components.Crypto.ManagedGCM
{
    internal class GcmUtilities
    {
        private const uint E1 = 0xe1000000;
        private const ulong E1UL = (ulong)E1 << 32;
        private const ulong M64 = 0x5555555555555555UL;
        private const ulong M64R = 0xAAAAAAAAAAAAAAAAUL;

        internal static void Expand64To128(ulong x, ulong[] z, int zOff)
        {
            // "shuffle" low half to even bits and high half to odd bits
            x = GcmUtilities.BitPermuteStep(x, 0x00000000FFFF0000UL, 16);
            x = GcmUtilities.BitPermuteStep(x, 0x0000FF000000FF00UL, 8);
            x = GcmUtilities.BitPermuteStep(x, 0x00F000F000F000F0UL, 4);
            x = GcmUtilities.BitPermuteStep(x, 0x0C0C0C0C0C0C0C0CUL, 2);
            x = GcmUtilities.BitPermuteStep(x, 0x2222222222222222UL, 1);

            z[zOff] = (x) & M64;
            z[zOff + 1] = (x >> 1) & M64;
        }

        internal static void Expand64To128Rev(ulong x, ulong[] z, int zOff)
        {
            // "shuffle" low half to even bits and high half to odd bits
            x = GcmUtilities.BitPermuteStep(x, 0x00000000FFFF0000UL, 16);
            x = GcmUtilities.BitPermuteStep(x, 0x0000FF000000FF00UL, 8);
            x = GcmUtilities.BitPermuteStep(x, 0x00F000F000F000F0UL, 4);
            x = GcmUtilities.BitPermuteStep(x, 0x0C0C0C0C0C0C0C0CUL, 2);
            x = GcmUtilities.BitPermuteStep(x, 0x2222222222222222UL, 1);

            z[zOff] = (x) & M64R;
            z[zOff + 1] = (x << 1) & M64R;
        }

        public static byte[] Clone(byte[] inp)
        {
            byte[] o = new byte[inp.Length];
            Buffer.BlockCopy(inp, 0, o, 0, inp.Length);
            return o;
        }
        internal static uint BitPermuteStep(uint x, uint m, int s)
        {
            uint t = (x ^ (x >> s)) & m;
            return (t ^ (t << s)) ^ x;
        }

        internal static ulong BitPermuteStep(ulong x, ulong m, int s)
        {
            ulong t = (x ^ (x >> s)) & m;
            return (t ^ (t << s)) ^ x;
        }


        internal static ulong BitPermuteStepSimple(ulong x, ulong m, int s)
        {
            return ((x & m) << s) | ((x >> s) & m);
        }
        internal static ulong[] OneAsUlongs()
        {
            ulong[] tmp = new ulong[2];
            tmp[0] = 1UL << 63;
            return tmp;
        }



        internal static void AsBytes(ulong[] x, byte[] z)
        {
            UInt64_To_BE(x, z, 0);
        }



        internal static ulong[] AsUlongs(byte[] x)
        {
            ulong[] z = new ulong[2];
            BE_To_UInt64(x, 0, z);
            return z;
        }

        internal static void AsUlongs(byte[] x, ulong[] z, int zOff)
        {
            BE_To_UInt64(x, 0, z, zOff, 2);
        }





        internal static void DivideP(ulong[] x, int xOff, ulong[] z, int zOff)
        {
            ulong x0 = x[xOff + 0], x1 = x[xOff + 1];
            ulong m = (ulong)((long)x0 >> 63);
            x0 ^= (m & E1UL);
            z[zOff + 0] = (x0 << 1) | (x1 >> 63);
            z[zOff + 1] = (x1 << 1) | (ulong)(-(long)m);
        }

        internal static void Multiply(byte[] x, byte[] y)
        {
            ulong[] t1 = AsUlongs(x);
            ulong[] t2 = AsUlongs(y);
            Multiply(t1, t2);
            AsBytes(t1, x);
        }



        internal static void Multiply(ulong[] x, ulong[] y)
        {
            //ulong x0 = x[0], x1 = x[1];
            //ulong y0 = y[0], y1 = y[1];
            //ulong z0 = 0, z1 = 0, z2 = 0;

            //for (int j = 0; j < 64; ++j)
            //{
            //    ulong m0 = (ulong)((long)x0 >> 63); x0 <<= 1;
            //    z0 ^= y0 & m0;
            //    z1 ^= y1 & m0;

            //    ulong m1 = (ulong)((long)x1 >> 63); x1 <<= 1;
            //    z1 ^= y0 & m1;
            //    z2 ^= y1 & m1;

            //    ulong c = (ulong)((long)(y1 << 63) >> 8);
            //    y1 = (y1 >> 1) | (y0 << 63);
            //    y0 = (y0 >> 1) ^ (c & E1UL);
            //}

            //z0 ^= z2 ^ (z2 >>  1) ^ (z2 >>  2) ^ (z2 >>  7);
            //z1 ^=      (z2 << 63) ^ (z2 << 62) ^ (z2 << 57);

            //x[0] = z0;
            //x[1] = z1;

            /*
             * "Three-way recursion" as described in "Batch binary Edwards", Daniel J. Bernstein.
             *
             * Without access to the high part of a 64x64 product x * y, we use a bit reversal to calculate it:
             *     rev(x) * rev(y) == rev((x * y) << 1) 
             */

            ulong x0 = x[0], x1 = x[1];
            ulong y0 = y[0], y1 = y[1];
            ulong x0r = GcmUtilities.Reverse(x0), x1r = GcmUtilities.Reverse(x1);
            ulong y0r = GcmUtilities.Reverse(y0), y1r = GcmUtilities.Reverse(y1);

            ulong h0 = GcmUtilities.Reverse(ImplMul64(x0r, y0r));
            ulong h1 = ImplMul64(x0, y0) << 1;
            ulong h2 = GcmUtilities.Reverse(ImplMul64(x1r, y1r));
            ulong h3 = ImplMul64(x1, y1) << 1;
            ulong h4 = GcmUtilities.Reverse(ImplMul64(x0r ^ x1r, y0r ^ y1r));
            ulong h5 = ImplMul64(x0 ^ x1, y0 ^ y1) << 1;

            ulong z0 = h0;
            ulong z1 = h1 ^ h0 ^ h2 ^ h4;
            ulong z2 = h2 ^ h1 ^ h3 ^ h5;
            ulong z3 = h3;

            z1 ^= z3 ^ (z3 >> 1) ^ (z3 >> 2) ^ (z3 >> 7);
            //          z2 ^=      (z3 << 63) ^ (z3 << 62) ^ (z3 << 57);
            z2 ^= (z3 << 62) ^ (z3 << 57);

            z0 ^= z2 ^ (z2 >> 1) ^ (z2 >> 2) ^ (z2 >> 7);
            z1 ^= (z2 << 63) ^ (z2 << 62) ^ (z2 << 57);

            x[0] = z0;
            x[1] = z1;
        }




        internal static void MultiplyP7(ulong[] x, int xOff, ulong[] z, int zOff)
        {
            ulong x0 = x[xOff + 0], x1 = x[xOff + 1];
            ulong c = x1 << 57;
            z[zOff + 0] = (x0 >> 7) ^ c ^ (c >> 1) ^ (c >> 2) ^ (c >> 7);
            z[zOff + 1] = (x1 >> 7) | (x0 << 57);
        }


        internal static void Square(ulong[] x, ulong[] z)
        {
            ulong[] t = new ulong[4];
            Expand64To128Rev(x[0], t, 0);
            Expand64To128Rev(x[1], t, 2);

            ulong z0 = t[0], z1 = t[1], z2 = t[2], z3 = t[3];

            z1 ^= z3 ^ (z3 >> 1) ^ (z3 >> 2) ^ (z3 >> 7);
            //          z2 ^=      (z3 << 63) ^ (z3 << 62) ^ (z3 << 57);
            z2 ^= (z3 << 62) ^ (z3 << 57);

            z0 ^= z2 ^ (z2 >> 1) ^ (z2 >> 2) ^ (z2 >> 7);
            z1 ^= (z2 << 63) ^ (z2 << 62) ^ (z2 << 57);

            z[0] = z0;
            z[1] = z1;
        }

        internal static void Xor(byte[] x, byte[] y)
        {
            int i = 0;
            do
            {
                x[i] ^= y[i]; ++i;
                x[i] ^= y[i]; ++i;
                x[i] ^= y[i]; ++i;
                x[i] ^= y[i]; ++i;
            }
            while (i < 16);
        }
        internal static void Xor16(byte[] x, byte[] y)
        {
            unsafe
            {

                {
                    fixed (byte* ptr1 = &y[0])
                    fixed (byte* b = &x[0])
                        *(long*)b ^= *(long*)ptr1;

                    fixed (byte* ptr1 = &y[8])
                    fixed (byte* b = &x[8])
                        *(long*)b ^= *(long*)ptr1;
                }
            }
        }

        internal static void Xor(byte[] x, byte[] y, int yOff, int yLen)
        {
            while (--yLen >= 0)
            {
                x[yLen] ^= y[yOff + yLen];
            }
        }

        internal static void Xor(byte[] x, int xOff, byte[] y, int yOff, int len)
        {
            while (--len >= 0)
            {
                x[xOff + len] ^= y[yOff + len];
            }
        }
        internal static void Xor16(byte[] x, int xOff, byte[] y, int yOff)
        {
            unsafe
            {

                {
                    fixed (byte* ptr1 = &y[yOff])
                    fixed (byte* b = &x[xOff])
                        *(long*)b ^= *(long*)ptr1;

                    fixed (byte* ptr1 = &y[yOff + 8])
                    fixed (byte* b = &x[xOff + 8])
                        *(long*)b ^= *(long*)ptr1;
                }
            }
            //while (--len >= 0)
            //{
            //    x[xOff + len] ^= y[yOff + len];
            //}
        }





        internal static void Xor(ulong[] x, int xOff, ulong[] y, int yOff, ulong[] z, int zOff)
        {
            z[zOff + 0] = x[xOff + 0] ^ y[yOff + 0];
            z[zOff + 1] = x[xOff + 1] ^ y[yOff + 1];
        }

        private static ulong ImplMul64(ulong x, ulong y)
        {
            ulong x0 = x & 0x1111111111111111UL;
            ulong x1 = x & 0x2222222222222222UL;
            ulong x2 = x & 0x4444444444444444UL;
            ulong x3 = x & 0x8888888888888888UL;

            ulong y0 = y & 0x1111111111111111UL;
            ulong y1 = y & 0x2222222222222222UL;
            ulong y2 = y & 0x4444444444444444UL;
            ulong y3 = y & 0x8888888888888888UL;

            ulong z0 = (x0 * y0) ^ (x1 * y3) ^ (x2 * y2) ^ (x3 * y1);
            ulong z1 = (x0 * y1) ^ (x1 * y0) ^ (x2 * y3) ^ (x3 * y2);
            ulong z2 = (x0 * y2) ^ (x1 * y1) ^ (x2 * y0) ^ (x3 * y3);
            ulong z3 = (x0 * y3) ^ (x1 * y2) ^ (x2 * y1) ^ (x3 * y0);

            z0 &= 0x1111111111111111UL;
            z1 &= 0x2222222222222222UL;
            z2 &= 0x4444444444444444UL;
            z3 &= 0x8888888888888888UL;

            return z0 | z1 | z2 | z3;
        }

        internal static void UInt32_To_BE(uint n, byte[] bs, int off)
        {
            bs[off] = (byte)(n >> 24);
            bs[off + 1] = (byte)(n >> 16);
            bs[off + 2] = (byte)(n >> 8);
            bs[off + 3] = (byte)(n);
        }

        internal static uint BE_To_UInt32(byte[] bs, int off)
        {
            return (uint)bs[off] << 24
                | (uint)bs[off + 1] << 16
                | (uint)bs[off + 2] << 8
                | (uint)bs[off + 3];
        }

        internal static void UInt64_To_BE(ulong n, byte[] bs, int off)
        {
            UInt32_To_BE((uint)(n >> 32), bs, off);
            UInt32_To_BE((uint)(n), bs, off + 4);
        }

        internal static void UInt64_To_BE(ulong[] ns, byte[] bs, int off)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                UInt64_To_BE(ns[i], bs, off);
                off += 8;
            }
        }

        internal static ulong BE_To_UInt64(byte[] bs, int off)
        {
            uint hi = BE_To_UInt32(bs, off);
            uint lo = BE_To_UInt32(bs, off + 4);
            return ((ulong)hi << 32) | (ulong)lo;
        }

        internal static void BE_To_UInt64(byte[] bs, int off, ulong[] ns)
        {
            for (int i = 0; i < ns.Length; ++i)
            {
                ns[i] = BE_To_UInt64(bs, off);
                off += 8;
            }
        }

        internal static void BE_To_UInt64(byte[] bs, int bsOff, ulong[] ns, int nsOff, int nsLen)
        {
            for (int i = 0; i < nsLen; ++i)
            {
                ns[nsOff + i] = BE_To_UInt64(bs, bsOff);
                bsOff += 8;
            }
        }

        internal static void UInt32_To_LE(uint n, byte[] bs)
        {
            bs[0] = (byte)(n);
            bs[1] = (byte)(n >> 8);
            bs[2] = (byte)(n >> 16);
            bs[3] = (byte)(n >> 24);
        }

        internal static void UInt32_To_LE(uint n, byte[] bs, int off)
        {
            //bs[off] = (byte)(n);
            //bs[off + 1] = (byte)(n >> 8);
            //bs[off + 2] = (byte)(n >> 16);
            //bs[off + 3] = (byte)(n >> 24);
            unsafe
            {
                fixed (byte* b = &bs[off])
                    *(uint*)b = n;
            }

        }

        internal static uint LE_To_UInt32(byte[] bs, int off)
        {
            unsafe
            {
                fixed (byte* ptr1 = &bs[off])
                    return *(uint*)ptr1;
            }
        }

        public static ulong Reverse(ulong i)
        {
            i = GcmUtilities.BitPermuteStepSimple(i, 0x5555555555555555UL, 1);
            i = GcmUtilities.BitPermuteStepSimple(i, 0x3333333333333333UL, 2);
            i = GcmUtilities.BitPermuteStepSimple(i, 0x0F0F0F0F0F0F0F0FUL, 4);
            return ReverseBytes(i);
        }

        public static ulong ReverseBytes(ulong i)
        {
            return RotateLeft(i & 0xFF000000FF000000UL, 8) |
                   RotateLeft(i & 0x00FF000000FF0000UL, 24) |
                   RotateLeft(i & 0x0000FF000000FF00UL, 40) |
                   RotateLeft(i & 0x000000FF000000FFUL, 56);
        }


        public static ulong RotateLeft(ulong i, int distance)
        {
            return (i << distance) ^ (i >> -distance);
        }
    }

    internal class Tables4kGcmMultiplier
    {
        private byte[] H;
        private ulong[] T;

        public void Init(byte[] H)
        {
            if (T == null)
            {
                T = new ulong[512];
            }
            else if (this.H.SequenceEqual(H))
            {
                return;
            }

            this.H = GcmUtilities.Clone(H);

            // T[0] = 0

            // T[1] = H.p^7
            GcmUtilities.AsUlongs(this.H, T, 2);
            GcmUtilities.MultiplyP7(T, 2, T, 2);

            for (int n = 2; n < 256; n += 2)
            {
                // T[2.n] = T[n].p^-1
                GcmUtilities.DivideP(T, n, T, n << 1);

                // T[2.n + 1] = T[2.n] + T[1]
                GcmUtilities.Xor(T, n << 1, T, 2, T, (n + 1) << 1);
            }
        }

        public void MultiplyH(byte[] x)
        {
            //ulong[] z = new ulong[2];
            //GcmUtilities.Copy(T, x[15] << 1, z, 0);
            //for (int i = 14; i >= 0; --i)
            //{
            //    GcmUtilities.MultiplyP8(z);
            //    GcmUtilities.Xor(z, 0, T, x[i] << 1);
            //}
            //Pack.UInt64_To_BE(z, x, 0);

            int pos = x[15] << 1;
            ulong z0 = T[pos + 0], z1 = T[pos + 1];

            for (int i = 14; i >= 0; --i)
            {
                pos = x[i] << 1;

                ulong c = z1 << 56;
                z1 = T[pos + 1] ^ ((z1 >> 8) | (z0 << 56));
                z0 = T[pos + 0] ^ (z0 >> 8) ^ c ^ (c >> 1) ^ (c >> 2) ^ (c >> 7);
            }

            GcmUtilities.UInt64_To_BE(z0, x, 0);
            GcmUtilities.UInt64_To_BE(z1, x, 8);
        }
    }
    internal class BasicGcmExponentiator
    {
        private ulong[] x;

        public void Init(byte[] x)
        {
            this.x = GcmUtilities.AsUlongs(x);
        }

        public void ExponentiateX(long pow, byte[] output)
        {
            // Initial value is little-endian 1
            ulong[] y = GcmUtilities.OneAsUlongs();

            if (pow > 0)
            {
                ulong[] powX = new ulong[2] { x[0], x[1] };
                do
                {
                    if ((pow & 1L) != 0)
                    {
                        GcmUtilities.Multiply(y, powX);
                    }
                    GcmUtilities.Square(powX, powX);
                    pow >>= 1;
                }
                while (pow > 0);
            }

            GcmUtilities.AsBytes(y, output);
        }
    }
}
