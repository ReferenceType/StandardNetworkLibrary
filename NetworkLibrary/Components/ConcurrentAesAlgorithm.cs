using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace NetworkLibrary.Components
{
    public class ConcurrentAesAlgorithm
    {
        ConcurrentBag<AesAlgorithm> algobag = new ConcurrentBag<AesAlgorithm>();
        private readonly byte[] key;
        private readonly byte[] IV;
        public ConcurrentAesAlgorithm(byte[] key, byte[] IV)
        {
            this.key = key;
            this.IV = IV;
            algobag.Add(new AesAlgorithm(key, IV));
        }

        private AesAlgorithm GetAlgorithm()
        {
            if (!algobag.TryTake(out var cry))
            {
                cry = new AesAlgorithm(key, IV);
            }
            return cry;
        }
        public byte[] Decrypt(byte[]bytes)=>Decrypt(bytes,0,bytes.Length);
        public byte[] Decrypt(byte[] bytes, int offset, int count)
        {

            var cry = GetAlgorithm();
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
            algobag.Add(cry);
            return res;
        }
        public int DecryptInto(byte[] source, int sourceOffset, int sourceCount, byte[] output, int outputOffset)
        {
            var cry = GetAlgorithm();
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
            algobag.Add(cry);
            return res;
        }

        public byte[] Encrypt(byte[] bytes) => Encrypt(bytes, 0, bytes.Length);
        public byte[] Encrypt(byte[] bytes, int offset, int count)
        {
            var cry = GetAlgorithm();
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
            algobag.Add(cry);
            return res;

        }
        public int EncryptInto(byte[] source, int sourceOffset, int sourceCount, byte[] output, int outputOffset)
        {
            var cry = GetAlgorithm();
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
            algobag.Add(cry);
            return res;
        }


    }
}
