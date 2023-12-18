using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using NetworkLibrary.Components.Crypto.Algorithms;
using NetworkLibrary.Utils;

namespace NetworkLibrary.Components.Crypto
{
    internal class AesManager:IAesEngine
    {
        private byte[] Key;
        private byte[] IV;
        private AesMode AesMode;

        ConcurrentBag<IAesAlgorithm> algorithms = new ConcurrentBag<IAesAlgorithm>();
        private byte header;

        public AesManager(byte[] Key,byte[] IV, AesMode mode) 
        {

            this.Key = Key;
            this.IV = IV;
            this.AesMode = mode;
            //algorithm = Create();
            header = GetHeader(AesMode);
        }
        private IAesAlgorithm GetAlgorithm()
        {
            if(!algorithms.TryTake(out var algorithm))
            {
                algorithm = Create();
            }
            return algorithm;
        }
        private void Return(IAesAlgorithm algorithm)
        {
            algorithms.Add(algorithm);
        }
        
        private IAesAlgorithm Create()
        {
            switch (AesMode)
            {
                case AesMode.CBCRandomIV:
                    return new AesCbcRandIVAlgorithm(Key, IV);
                case AesMode.GCM:
                    //return new AesGcmManagedAlgorithm(Key, IV);
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
                    return new AesGcmAlgorithm(Key,IV);
#endif
                    return new AesGcmManagedAlgorithm(Key,IV);
                case AesMode.CBCCtrIV:
                    return new AesCbcCtrIVAlgorithm(Key, IV);
                case AesMode.CBCCtrIVHMAC:
                    return new AesCbcHmacCtrAlgorithm(Key, IV);
                case AesMode.None:
                    return new NoEncyption();

                default: throw new NotImplementedException();
            }
        }

        public byte[] Decrypt(byte[] bytes)
        {
            var alg = GetAlgorithm();
            var res = alg.Decrypt(bytes);
            Return(alg); 
            return res;
        }

        public byte[] Decrypt(byte[] bytes, int offset, int count)
        {
            var alg = GetAlgorithm();
            var res = alg.Decrypt(bytes, offset, count);
            Return(alg);
            return res;
        }

        public int DecryptInto(byte[] source, int sourceOffset, int sourceCount, byte[] output, int outputOffset)
        {
            var alg = GetAlgorithm();
            var res = alg.DecryptInto(source, sourceOffset, sourceCount, output, outputOffset);
            Return(alg);
            return res;
        }

        public byte[] Encrypt(byte[] bytes)
        {
            return Encrypt(bytes, 0, bytes.Length);
        }

        public byte[] Encrypt(byte[] bytes, int offset, int count)
        {
            byte[] res;
            var b = BufferPool.RentBuffer(count + 256);
            int am = EncryptInto(bytes, offset, count, b, 0);
            res = ByteCopy.ToArray(b, 0, am);
            BufferPool.ReturnBuffer(b);
            return res;
        }

        public int EncryptInto(byte[] source, int sourceOffset, int sourceCount, byte[] output, int outputOffset)
        {
            output[outputOffset++] = header;
            var alg = GetAlgorithm();
            var res = alg.EncryptInto(source, sourceOffset, sourceCount, output, outputOffset) + 1;
            Return(alg);
            return res;
        }

        public int EncryptInto(byte[] data1, int offset1, int count1, byte[] data2, int offset2, int count2, byte[] output, int outputOffset)
        {
            output[outputOffset++] = header;
            var alg = GetAlgorithm();
            var res = alg.EncryptInto(data1, offset1, count1, data2, offset2, count2, output, outputOffset) + 1;
            Return(alg);
            return res;
        }

        byte GetHeader(AesMode mode)
        {
            return (byte)mode;
        }
    }
}
