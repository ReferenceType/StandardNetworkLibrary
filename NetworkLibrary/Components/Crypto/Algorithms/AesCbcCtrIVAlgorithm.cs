using System;
using System.Security.Cryptography;
using System.Threading;
using NetworkLibrary.Utils;

namespace NetworkLibrary.Components.Crypto.Algorithms
{
    internal class AesCbcCtrIVAlgorithm : IAesAlgorithm
    {
        public int DecryptorInputBlockSize => 16;

        public int DecryptorOutputBlockSize => 16;

        public int EncryptorInputBlockSize => 16;

        public int EncryptorOutputBlockSize => 16;

        private long ctr;

        AesCbcAlgorithm cbc;
        AesCbcAlgorithm counterEncryptor;
        public AesCbcCtrIVAlgorithm(byte[] Key, byte[] IV)
        {
            cbc = new AesCbcAlgorithm(Key, IV);
            counterEncryptor = new AesCbcAlgorithm(Key, IV);
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
            var iv = ParseIv(source, sourceOffset, out int count);
            sourceCount -= count;
            sourceOffset += count;
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
            //rng.GetBytes(output, outputOffset, IvSize);
            int oo = outputOffset;
            PrimitiveEncoder.WriteInt64(output, ref oo, Interlocked.Increment(ref ctr));
            int delta = oo - outputOffset;

            byte[] iv = ToIVArray(output, outputOffset, delta);
            cbc.ApplyIVEncryptor(iv);

            outputOffset += delta;

            int res = cbc.EncryptInto(source, sourceOffset, sourceCount, output, outputOffset);
            return res + delta;
        }

        public int EncryptInto(byte[] data1, int offset1, int count1, byte[] data2, int offset2, int count2, byte[] output, int outputOffset)
        {
            int oo = outputOffset;
            PrimitiveEncoder.WriteInt64(output, ref oo, Interlocked.Increment(ref ctr));
            int delta = oo - outputOffset;

            byte[] iv = ToIVArray(output, outputOffset, delta);
            cbc.ApplyIVEncryptor(iv);

            outputOffset += delta;

            int res = cbc.EncryptInto(data1, offset1, count1, data2, offset2, count2, output, outputOffset);
            return res + delta;
        }

        public int GetEncriptorOutputSize(int inputSize)
        {
            throw new NotImplementedException();
        }

        // encoded integer here
        private byte[] ToIVArray(byte[] buffer, int v1, int v)
        {
            // return IV;
            return counterEncryptor.Encrypt(buffer, v1, v);

        }

        private byte[] ParseIv(byte[] buffer, int v1, out int count)
        {

            int oo = v1;
            _ = PrimitiveEncoder.ReadInt64(buffer, ref oo);
            count = oo - v1;

            return counterEncryptor.Encrypt(buffer, v1, count);

        }
    }
}
