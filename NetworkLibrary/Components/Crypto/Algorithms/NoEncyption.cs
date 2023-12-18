using NetworkLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkLibrary.Components.Crypto.Algorithms
{
    internal class NoEncyption : IAesAlgorithm
    {
        public int DecryptorInputBlockSize => 0;

        public int DecryptorOutputBlockSize => 0;

        public int EncryptorInputBlockSize => 0;

        public int EncryptorOutputBlockSize => 0;

        public byte[] Decrypt(byte[] bytes)
        {
            return bytes;
        }

        public byte[] Decrypt(byte[] bytes, int offset, int count)
        {
            return ByteCopy.ToArray(bytes, offset, count);
        }

        public int DecryptInto(byte[] source, int sourceOffset, int sourceCount, byte[] output, int outputOffset)
        {
            ByteCopy.BlockCopy(source, sourceOffset, output, outputOffset, sourceCount);
            return sourceCount;
        }

        public void Dispose()
        {

        }

        public byte[] Encrypt(byte[] bytes)
        {
            return Encrypt(bytes, 0, bytes.Length);
        }

        public byte[] Encrypt(byte[] bytes, int offset, int count)
        {
            var buf = BufferPool.RentBuffer(count);
            ByteCopy.BlockCopy(bytes, offset, buf, 0, count);
            var res = ByteCopy.ToArray(buf, 0, count);
            BufferPool.ReturnBuffer(buf);
            return res;
        }

        public int EncryptInto(byte[] source, int sourceOffset, int sourceCount, byte[] output, int outputOffset)
        {

            ByteCopy.BlockCopy(source, sourceOffset, output, outputOffset, sourceCount);
            return sourceCount;
        }

        public int EncryptInto(byte[] data1, int offset1, int count1, byte[] data2, int offset2, int count2, byte[] output, int outputOffset)
        {
            ByteCopy.BlockCopy(data1, offset1, output, outputOffset, count1);
            ByteCopy.BlockCopy(data2, offset2, output, outputOffset + count1, count2);
            return count1 + count2;
        }

        public int GetEncriptorOutputSize(int inputSize)
        {
            return inputSize;
        }
    }
}
