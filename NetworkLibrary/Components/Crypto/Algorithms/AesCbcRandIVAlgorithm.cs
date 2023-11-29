using System;
using System.Security.Cryptography;
using NetworkLibrary.Utils;

namespace NetworkLibrary.Components.Crypto.Algorithms
{
    internal class AesCbcRandIVAlgorithm : IAesAlgorithm
    {
        public int DecryptorInputBlockSize => 16;

        public int DecryptorOutputBlockSize => 16;

        public int EncryptorInputBlockSize => 16;

        public int EncryptorOutputBlockSize => 16;
        [ThreadStatic]
        private static byte[] IVgen;
        const int IvSize = 16;

        private RandomNumberGenerator rng = RandomNumberGenerator.Create();
        byte[] Key, IV;
        AesCbcAlgorithm cbc;
        public AesCbcRandIVAlgorithm(byte[] Key, byte[] IV)
        {
            this.Key = Key;
            this.IV = IV;
            cbc = new AesCbcAlgorithm(Key, IV);
        }

        public byte[] Decrypt(byte[] message)
        {
            return Decrypt(message, 0, message.Length);
        }

        public byte[] Decrypt(byte[] bytes, int offset, int count)
        {
            byte[] res;
            var buffer = BufferPool.RentBuffer(count + 256);
            int amount = DecryptInto(bytes, offset, count, buffer, 0);
            res = ByteCopy.ToArray(buffer, 0, amount);
            BufferPool.ReturnBuffer(buffer);
            return res;
        }

        public int DecryptInto(byte[] source, int sourceOffset, int sourceCount, byte[] output, int outputOffset)
        {
            var iv = GetIV(source, sourceOffset, IvSize);
            sourceCount -= IvSize;
            sourceOffset += IvSize;
            cbc.ApplyIVDecryptor(iv);
            int res = cbc.DecryptInto(source, sourceOffset, sourceCount, output, outputOffset);

            return res;
        }

        public void Dispose()
        {
            cbc.Dispose();
        }

        public byte[] Encrypt(byte[] message)
        {
            return Encrypt(message, 0, message.Length);
        }

        public byte[] Encrypt(byte[] bytes, int offset, int count)
        {
            byte[] res;
            var buffer = BufferPool.RentBuffer(count + 256);
            int amount = EncryptInto(bytes, offset, count, buffer, 0);
            res = ByteCopy.ToArray(buffer, 0, amount);
            BufferPool.ReturnBuffer(buffer);
            return res;
        }

        public int EncryptInto(byte[] source, int sourceOffset, int sourceCount, byte[] output, int outputOffset)
        {
            rng.GetBytes(output, outputOffset, IvSize);
            byte[] iv = GetIV(output, outputOffset, IvSize);
            cbc.ApplyIVEncryptor(iv);

            outputOffset += IvSize;

            int res = cbc.EncryptInto(source, sourceOffset, sourceCount, output, outputOffset);
            return res + IvSize;
        }

        public int EncryptInto(byte[] data1, int offset1, int count1, byte[] data2, int offset2, int count2, byte[] output, int outputOffset)
        {
            rng.GetBytes(output, outputOffset, IvSize);
            byte[] iv = GetIV(output, outputOffset, IvSize);
            cbc.ApplyIVEncryptor(iv);

            outputOffset += IvSize;

            int res = cbc.EncryptInto(data1, offset1, count1, data2, offset2, count2, output, outputOffset);
            return res + IvSize;
        }

        public int GetEncriptorOutputSize(int inputSize)
        {
            throw new NotImplementedException();
        }

        private byte[] GetIV(byte[] buffer, int v1, int v2)
        {
            if (IVgen == null)
                IVgen = new byte[IvSize];
            ByteCopy.BlockCopy(buffer, v1, IVgen, 0, IvSize);
            return IVgen;

        }
    }
}