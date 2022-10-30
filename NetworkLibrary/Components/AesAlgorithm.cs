using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace NetworkLibrary.Components
{
    public class AesAlgorithm:IDisposable
    {
        protected Aes algorithm;
        protected ICryptoTransform encryptor;
        protected ICryptoTransform decryptor;

        public int EncryptorInputBlockSize { get => encryptor.InputBlockSize; }
        public int EncryptorOutputBlockSize { get => encryptor.OutputBlockSize; }
        public int DecryptorInputBlockSize { get => decryptor.InputBlockSize; }
        public int DecryptorOutputBlockSize { get => decryptor.OutputBlockSize; }

        private byte[] finalBlock= new byte[0];
        public AesAlgorithm(byte[] Key, byte[] IV,string algorithmName = null)
        {
            if(algorithmName == null)
            {
                algorithm = Aes.Create();
            }
            else
            {
                algorithm = Aes.Create(algorithmName);
            }
            
            algorithm.Key = Key;
            algorithm.IV = IV;

            encryptor = algorithm.CreateEncryptor(algorithm.Key, algorithm.IV);
            decryptor = algorithm.CreateDecryptor(algorithm.Key, algorithm.IV);
        }

        public int GetEncriptorOutputSize(int inputSize)
        {
            return inputSize + (EncryptorOutputBlockSize - (inputSize % EncryptorOutputBlockSize));
        }

        /// <summary>
        /// Decrypts a message into new byte array
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        /// 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] Decrypt(byte[] message)
        {
            byte[] output = decryptor.TransformFinalBlock(message, 0, message.Length);
            return output;

        }
        /// <summary>
        /// Decrypts a message from buffer region into new byte array
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        /// 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] Decrypt(byte[] buffer, int offset, int count)
        {
            
            byte[] output = decryptor.TransformFinalBlock(buffer, offset, count);
            return output;

        }

        /// <summary>
        /// Decryps a message partially into output buffer without the final block.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="sourceOffset"></param>
        /// <param name="sourceCount"></param>
        /// <param name="output"></param>
        /// <param name="outputOffset"></param>
        /// <returns></returns>
        /// 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int PartialDecrpytInto(byte[] source, int sourceOffset, int sourceCount, byte[] output, int outputOffset)
        {
            return decryptor.TransformBlock(source, sourceOffset, sourceCount, output, outputOffset);

        }

        /// <summary>
        /// Decrypts a message into output destination array fully.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="sourceOffset"></param>
        /// <param name="sourceCount"></param>
        /// <param name="output"></param>
        /// <param name="outputOffset"></param>
        /// <returns></returns>
        /// 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DecryptInto(byte[] source, int sourceOffset, int sourceCount, byte[] output, int outputOffset)
        {
            int amountDecripted = decryptor.TransformBlock(source, sourceOffset, sourceCount, output, outputOffset);

            var lastChunk = Decrypt(finalBlock);

            if (lastChunk.Length != 0)
            {
                Buffer.BlockCopy(lastChunk, 0, output, outputOffset + amountDecripted, lastChunk.Length);
                amountDecripted += lastChunk.Length;
            }

            return amountDecripted;
        }

        /// <summary>
        /// Encrypts a message and retuns the encripted array as a new byte array
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        /// 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] Encrypt(byte[] message)
        {
            return encryptor.TransformFinalBlock(message, 0, message.Length);

        }

        /// <summary>
        /// Encrypts a message from buffer region and retuns the encripted array as a new byte array
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] Encrypt(byte[] buffer, int offset, int count)
        {
            return encryptor.TransformFinalBlock(buffer, offset, count);

        }

        /// <summary>
        /// Encrypts a message partially into output buffer without the final block.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="sourceOffset"></param>
        /// <param name="sourceCount"></param>
        /// <param name="output"></param>
        /// <param name="outputOffset"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int PartialEncrpytInto(byte[] source, int sourceOffset, int sourceCount, byte[] output, int outputOffset)
        {
            return encryptor.TransformBlock(source, sourceOffset, sourceCount, output, outputOffset);

        }
        /// <summary>
        /// Decrypts a message into output destination array fully.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="sourceOffset"></param>
        /// <param name="sourceCount"></param>
        /// <param name="output"></param>
        /// <param name="outputOffset"></param>
        /// <returns></returns>
        /// 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int EncryptInto(byte[] source, int sourceOffset, int sourceCount, byte[] output, int outputOffset)
        {
            // it has to  be multiple of block size to do partial.
            if (sourceCount < EncryptorOutputBlockSize)
            {
                byte[] oneShot = Encrypt(source, sourceOffset, sourceCount);
                Buffer.BlockCopy(oneShot, 0, output, outputOffset, oneShot.Length);
                return oneShot.Length;
            }

            int partialTransformAmount = sourceCount - sourceCount % EncryptorOutputBlockSize;
            int amountDecripted = encryptor.TransformBlock(source, sourceOffset, partialTransformAmount, output, outputOffset);

            byte[] lastChunk = Encrypt(source, sourceOffset + amountDecripted, sourceCount % EncryptorOutputBlockSize);

            if (lastChunk.Length != 0)
            {
                Buffer.BlockCopy(lastChunk, 0, output, outputOffset + amountDecripted, lastChunk.Length);
                amountDecripted += lastChunk.Length;
            }

            return amountDecripted;
        }

        public void Dispose()
        {
            encryptor.Dispose();
            decryptor.Dispose();
        }
    }
}
