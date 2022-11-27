using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace NetworkLibrary.Components
{
    public class ConcurrentAesAlgorithm
    {
        private readonly ConcurrentBag<AesAlgorithm> algorithmPool = new ConcurrentBag<AesAlgorithm>();
        private readonly byte[] key;
        private readonly byte[] IV;
        public ConcurrentAesAlgorithm(byte[] key, byte[] IV)
        {
            this.key = key;
            this.IV = IV;
            algorithmPool.Add(new AesAlgorithm(key, IV));
        }

        private AesAlgorithm RentAlgorithm()
        {
            if (!algorithmPool.TryTake(out var cry))
            {
                cry = new AesAlgorithm(key, IV);
            }
            return cry;
        }

        private void ReturnAlgorithm(AesAlgorithm alg)
        {
            algorithmPool.Add(alg);

        }
        public byte[] Decrypt(byte[]bytes)=>Decrypt(bytes,0,bytes.Length);
        public byte[] Decrypt(byte[] bytes, int offset, int count)
        {

            var cry = RentAlgorithm();
            byte[] res;
            try
            {
                res = cry.Decrypt(bytes, offset, count);

            }
            catch
            {
                cry.Dispose();
                throw;
            }
            ReturnAlgorithm(cry);
            return res;
        }
        public int DecryptInto(byte[] source, int sourceOffset, int sourceCount, byte[] output, int outputOffset)
        {
            var cry = RentAlgorithm();
            int res;
            try
            {
                res = cry.DecryptInto(source, sourceOffset, sourceCount, output, outputOffset);

            }
            catch
            {
                cry.Dispose();
                throw;
            }
            ReturnAlgorithm(cry);
            return res;
        }

        public byte[] Encrypt(byte[] bytes) => Encrypt(bytes, 0, bytes.Length);
        public byte[] Encrypt(byte[] bytes, int offset, int count)
        {
            var cry = RentAlgorithm();
            byte[] res;
            try
            {
                res = cry.Encrypt(bytes, offset, count);

            }
            catch
            {
                cry.Dispose();
                throw;
            }
            ReturnAlgorithm(cry);
            return res;

        }
        public int EncryptInto(byte[] source, int sourceOffset, int sourceCount, byte[] output, int outputOffset)
        {
            var cry = RentAlgorithm();
            int res;
            try
            {
                 res = cry.EncryptInto(source, sourceOffset, sourceCount, output, outputOffset);

            }
            catch
            {
                cry.Dispose();
                throw;
            }
            ReturnAlgorithm(cry);
            return res;
        }


    }
}
