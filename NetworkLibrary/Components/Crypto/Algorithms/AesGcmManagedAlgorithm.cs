using NetworkLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace NetworkLibrary.Components.Crypto.Algorithms
{
    internal class AesGcmManagedAlgorithm : IAesAlgorithm
    {
        const int n = 12;
        [ThreadStatic]
        static byte[] nonce = new byte[n];
        const int _macSize = 14;
        private byte[] key;
        AesGcmManaged ciperDecrypt;
        AesGcmManaged ciperEncrypt;
        byte[] iv;
        public AesGcmManagedAlgorithm(byte[] key, byte[] IV)
        {
            iv = IV;
            this.key = key;
            ciperEncrypt = new AesGcmManaged(new AesCipher(), _macSize, key, true);
            ciperDecrypt = new AesGcmManaged(new AesCipher(), _macSize, key, false);
        }

        public int DecryptorInputBlockSize => 16;

        public int DecryptorOutputBlockSize => 16;

        public int EncryptorInputBlockSize => 16;

        public int EncryptorOutputBlockSize => 16;

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
        long ctr = 0;
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

        public int DecryptInto(byte[] source, int sourceOffset, int sourceCount, byte[] output, int outputOffset)
        {
            var nonce = ParseNonce(source, ref sourceOffset, ref sourceCount);


            ciperDecrypt.Init(nonce);

            var len = ciperDecrypt.ProcessBytes(source, sourceOffset, sourceCount, output, outputOffset);
            int f = ciperDecrypt.DoFinal(output, outputOffset + len);
            //cipher.Reset();
            return len + f;
        }

        public void Dispose()
        {

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

        public int EncryptInto(byte[] source, int sourceOffset, int sourceCount, byte[] output, int outputOffset)
        {
            var nonce = GenerateNextNonce(out int n);
            ciperEncrypt.Init(nonce);

            var len = ciperEncrypt.ProcessBytes(source, sourceOffset, sourceCount, output, outputOffset + n);
            int f = ciperEncrypt.DoFinal(output, outputOffset + n + len);

            Buffer.BlockCopy(nonce, 4, output, outputOffset, n);
            return len + f + n;
        }



        public int EncryptInto(byte[] data1, int offset1, int count1, byte[] data2, int offset2, int count2, byte[] output, int outputOffset)
        {
            var nonce = GenerateNextNonce(out int n);
            ciperEncrypt.Init(nonce);

            var len = ciperEncrypt.ProcessBytes(data1, offset1, count1, output, outputOffset + n);
            var len2 = ciperEncrypt.ProcessBytes(data2, offset2, count2, output, outputOffset + n + len);
            int f = ciperEncrypt.DoFinal(output, outputOffset + n + len + len2);

            Buffer.BlockCopy(nonce, 4, output, outputOffset, n);
            return len + len2 + f + n;
        }

        public int GetEncriptorOutputSize(int inputSize)
        {
            return inputSize + _macSize / 8 + 12;
        }



    }
}
