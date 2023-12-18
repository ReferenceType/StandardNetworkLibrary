
using NetworkLibrary.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace NetworkLibrary.Components.Crypto.Algorithms
{

#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
    internal class AesGcmAlgorithm : IAesAlgorithm
    {

        public int DecryptorInputBlockSize => 16;

        public int DecryptorOutputBlockSize => 16;

        public int EncryptorInputBlockSize => 16;

        public int EncryptorOutputBlockSize => 16;
        byte[] iv;

        private AesGcm aes;
        private AesGcm aes2;
        const int nonceSize = 12;
        const int tagSize = 14;
        public AesGcmAlgorithm(byte[] Key, byte[] IV)
        {
            aes = new AesGcm(Key);
            aes2 = new AesGcm(Key);
            iv = IV;
        }

        public byte[] Decrypt(byte[] message)
        {
            return Decrypt(message, 0, message.Length);
        }

        public byte[] Decrypt(byte[] buffer, int offset, int count)
        {
            var buff = BufferPool.RentBuffer(count + 256);
            int am = DecryptInto(buffer, offset, count, buff, 0);
            var res = ByteCopy.ToArray(buff, 0, am);
            BufferPool.ReturnBuffer(buff);
            return res;
        }

        public void Dispose()
        {
            aes?.Dispose();
            aes2?.Dispose();
        }

        public byte[] Encrypt(byte[] message)
        {
            return Encrypt(message, 0, message.Length);
        }

        public byte[] Encrypt(byte[] buffer, int offset, int count)
        {
            var buff = BufferPool.RentBuffer(count + 256);
            int am = EncryptInto(buffer, offset, count, buff, 0);
            var res = ByteCopy.ToArray(buff, 0, am);
            BufferPool.ReturnBuffer(buff);
            return res;
        }

        public int GetEncriptorOutputSize(int inputSize)
        {
            return nonceSize + tagSize + inputSize;
        }

        public int EncryptInto(byte[] data, int offset, int count, byte[] output, int outputOffset)
        {
            int cipherSize = count;
            var non = GenerateNextNonce(out int nonceSize);

            int encryptedDataLength = nonceSize + tagSize + cipherSize;
            Span<byte> encryptedData;
            unsafe
            {
                fixed (byte* buffer = &output[outputOffset])
                {
                    encryptedData = new Span<byte>(buffer, encryptedDataLength);
                }
            }
            Buffer.BlockCopy(non, 4, output, outputOffset, nonceSize);

            var nonce = encryptedData.Slice(0, nonceSize);
            var tag = encryptedData.Slice(nonceSize + cipherSize, tagSize);
            var cipherBytes = encryptedData.Slice(nonceSize, cipherSize);

            // RandomNumberGenerator.Fill(nonce);

            unsafe
            {
                fixed (byte* buffer = &data[offset])
                    aes.Encrypt(non, new ReadOnlySpan<byte>(buffer, count), cipherBytes, tag);
            }

            return encryptedDataLength;
        }
        public int EncryptInto(byte[] data, int offset, int count,
         byte[] data1, int offset1, int count1, byte[] output, int outputOffset)
        {
            {
                var non = GenerateNextNonce(out int nonceSize);

                int cipherSize = count + count1;

                int encryptedDataLength = tagSize + nonceSize + cipherSize;
                Span<byte> encryptedData = new Span<byte>(output);
                unsafe
                {
                    fixed (byte* buffer = &output[outputOffset])
                    {
                        encryptedData = new Span<byte>(buffer, encryptedDataLength);
                    }
                }
                Buffer.BlockCopy(non, 4, output, outputOffset, nonceSize);

                var nonce = encryptedData.Slice(0, nonceSize);
                var tag = encryptedData.Slice(nonceSize + cipherSize, tagSize);
                var cipherBytes = encryptedData.Slice(nonceSize, cipherSize);

                byte[] b = BufferPool.RentBuffer(cipherSize);
                Buffer.BlockCopy(data, offset, b, 0, count);
                Buffer.BlockCopy(data1, offset1, b, count, count1);
                // Generate secure nonce

                unsafe
                {
                    fixed (byte* buffer = &b[0])
                        aes.Encrypt(non, new ReadOnlySpan<byte>(buffer, cipherSize), cipherBytes, tag);

                }
                BufferPool.ReturnBuffer(b);
                return encryptedDataLength;
            }
        }
        public int DecryptInto(byte[] data, int offset, int count, byte[] output, int outputOffset)
        {
            Span<byte> encryptedData;
            unsafe
            {
                fixed (byte* buffer = &data[offset])
                {
                    encryptedData = new Span<byte>(buffer, count);
                }
            }

            int oldOff = offset;
            var nonce = ParseNonce(data, ref offset, ref count).AsSpan();
            int nonceSize = offset - oldOff;

            int cipherSize = encryptedData.Length - nonceSize - tagSize;

            // var nonce = encryptedData.Slice(0, nonceSize);
            var tag = encryptedData.Slice(nonceSize + cipherSize, tagSize);
            var cipherBytes = encryptedData.Slice(nonceSize, cipherSize);

            unsafe
            {
                fixed (byte* buffer = &output[outputOffset])
                {
                    aes2.Decrypt(nonce, cipherBytes, tag, new Span<byte>(buffer, cipherSize));
                }
            }

            return cipherSize;
        }


        [ThreadStatic]
        static byte[] nonce;
        long ctr = 0;

        private byte[] GetEmptyNonceArray()
        {
            if (true || nonce == null)
            {
                nonce = new byte[12];
                nonce[0] = iv[0];
                nonce[1] = iv[1];
                nonce[2] = iv[2];
                nonce[3] = iv[3];
                return nonce;

            }
            int off = 4;
            PrimitiveEncoder.WriteFixedInt64(nonce, ref off, 0);
            return nonce;
        }
        private byte[] GenerateNextNonce(out int encodedCount)
        {
            long n = Interlocked.Increment(ref ctr);
            var nonce = GetEmptyNonceArray();// fill random 4 in begining and 8 0s after
            int off = 4;

            PrimitiveEncoder.WriteInt64(nonce, ref off, n);// encode counter
            encodedCount = off - 4;
            return nonce;
        }
        private byte[] ParseNonce(byte[] source, ref int sourceOffset, ref int sourceCount)
        {
            var nonce = GetEmptyNonceArray();
            int oldOff = sourceOffset;
            PrimitiveEncoder.ReadInt64(source, ref sourceOffset);

            int n = sourceOffset - oldOff;
            Buffer.BlockCopy(source, oldOff, nonce, 4, n);
            sourceCount -= n;

            return nonce;
        }

    }
#endif

}
