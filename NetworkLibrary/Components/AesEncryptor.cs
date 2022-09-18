using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace NetworkLibrary.Components
{
    public class AesEncryptor
    {
        protected Aes algorithm;
        protected ICryptoTransform encryptor;
        protected ICryptoTransform decryptor;
        public AesEncryptor(byte[] Key, byte[] IV)
        {
            algorithm = Aes.Create();
            algorithm.Key = Key;
            algorithm.IV = IV;

            encryptor = algorithm.CreateEncryptor(algorithm.Key, algorithm.IV);
            decryptor = algorithm.CreateDecryptor(algorithm.Key, algorithm.IV);
        }

        public byte[] Encrypt(byte[] message)
        {
            return encryptor.TransformFinalBlock(message, 0, message.Length);

        }

        public byte[] Encrypt(byte[] buffer, int offset, int count)
        {
            return encryptor.TransformFinalBlock(buffer, offset, count);

        }

        public byte[] Decrypt(byte[] message)
        {
            byte[] output = decryptor.TransformFinalBlock(message, 0, message.Length);
            return output;

        }
        public byte[] Decrypt(byte[] buffer,int offset, int count)
        {
            byte[] output = decryptor.TransformFinalBlock(buffer, offset, count);
            return output;

        }

        public int DecryptInto(byte[] source, int sourceOffset, int sourceCount, byte[] output, int outputOffset)
        {
            int amountDecripted = decryptor.TransformBlock(source, sourceOffset, sourceCount, output, outputOffset);

            var lastChunk = Decrypt(new byte[0]);

            if (lastChunk.Length != 0)
            {
                Buffer.BlockCopy(lastChunk, 0, source, sourceOffset + amountDecripted, lastChunk.Length);
                amountDecripted += lastChunk.Length;
            }

            return amountDecripted;
        }

        public int EncryptInto(byte[] source, int sourceOffset, int sourceCount, byte[] output, int outputOffset)
        {
            int amountDecripted = encryptor.TransformBlock(source, sourceOffset, sourceCount, output, outputOffset);

            var lastChunk = Encrypt(new byte[0]);

            if (lastChunk.Length != 0)
            {
                Buffer.BlockCopy(lastChunk, 0, source, sourceOffset + amountDecripted, lastChunk.Length);
                amountDecripted += lastChunk.Length;
            }

            return amountDecripted;
        }
    }
}
