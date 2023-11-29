using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace NetworkLibrary.Components.Crypto.Algorithms
{
    public class AesCbcAlgorithm
        : IDisposable, IAesAlgorithm
    {
        private readonly SymmetricAlgorithm algorithm;
        private ICryptoTransform encryptor;
        private ICryptoTransform decryptor;

        public int EncryptorInputBlockSize { get => encryptor.InputBlockSize; }
        public int EncryptorOutputBlockSize { get => encryptor.OutputBlockSize; }
        public int DecryptorInputBlockSize { get => decryptor.InputBlockSize; }
        public int DecryptorOutputBlockSize { get => decryptor.OutputBlockSize; }

        private readonly byte[] finalBlock = new byte[0];
        public AesCbcAlgorithm(byte[] Key, byte[] IV, string algorithmName = null)
        {
            if (algorithmName == null)
            {
#if UNITY_STANDALONE
                // AES.create is not compatible with Unity... 
                algorithm = new System.Security.Cryptography.RijndaelManaged();
#else
                // AES.create is not compatible with Unity... 
                // algorithm = Aes.Create();
                algorithm = Aes.Create();
                //algorithm = new System.Security.Cryptography.RijndaelManaged();

#endif
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
            return inputSize + (EncryptorOutputBlockSize - inputSize % EncryptorOutputBlockSize);
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

        public int EncryptInto(byte[] data1, int offset1, int count1, byte[] data2, int offset2, int count2, byte[] output, int outputOffset)
        {
            //if ((data1 == null))
            //{
            //    throw new ArgumentNullException(nameof(data1));
            //}
            //if ((data2 == null))
            //{
            //    throw new Exception ("data2");
            //}
            const int EncryptorOutputBlockSize = 16;
            if (count1 < EncryptorOutputBlockSize)
            {
                if (count2 + count1 < EncryptorOutputBlockSize)
                {
                    var total = new byte[count1 + count2];
                    Buffer.BlockCopy(data1, offset1, total, 0, count1);
                    Buffer.BlockCopy(data2, offset2, total, count1, count2);
                    var res = encryptor.TransformFinalBlock(total, 0, total.Length);
                    Buffer.BlockCopy(res, 0, output, outputOffset, res.Length);
                    return res.Length;
                }
                else
                {
                    var mid = new byte[EncryptorOutputBlockSize];
                    Buffer.BlockCopy(data1, offset1, mid, 0, count1);
                    int rem = EncryptorOutputBlockSize - count1;
                    Buffer.BlockCopy(data2, offset2, mid, count1, rem);
                    offset2 += rem;
                    count2 -= rem;
                    data1 = mid;
                    offset1 = 0;
                    count1 = EncryptorOutputBlockSize;
                }
            }

            int amountEnc = 0;
            int am = 0;

            // first
            int firstRemainder = count1 % EncryptorOutputBlockSize;
            int amountToEnc1 = count1 - firstRemainder;
            am = encryptor.TransformBlock(data1, offset1, amountToEnc1, output, outputOffset);
            amountEnc += am;
            outputOffset += am;

            if (firstRemainder != 0)
            {
                // we dont know if we complete to EncryptorOutputBlockSize with 2nd
                int rem = EncryptorOutputBlockSize - firstRemainder;
                if (count2 >= rem)
                {
                    var mid = new byte[EncryptorOutputBlockSize];
                    Buffer.BlockCopy(data1, offset1 + amountToEnc1, mid, 0, firstRemainder);
                    Buffer.BlockCopy(data2, offset2, mid, firstRemainder, rem);
                    offset2 += rem;
                    count2 -= rem;

                    am = encryptor.TransformBlock(mid, 0, EncryptorOutputBlockSize, output, outputOffset);
                    amountEnc += am;
                    outputOffset += am;
                }
                else// unable to create 16 block
                {
                    var mid = new byte[firstRemainder + count2];
                    Buffer.BlockCopy(data1, offset1 + amountToEnc1, mid, 0, firstRemainder);
                    Buffer.BlockCopy(data2, offset2, mid, firstRemainder, count2);

                    var fin = encryptor.TransformFinalBlock(mid, 0, mid.Length);
                    fin.CopyTo(output, outputOffset);
                    return amountEnc + fin.Length;
                }

            }
            // second
            if (count2 < EncryptorOutputBlockSize)
            {
                byte[] last = encryptor.TransformFinalBlock(data2, offset2, count2);
                Buffer.BlockCopy(last, 0, output, outputOffset, last.Length);
                return amountEnc + last.Length;
            }

            int secondRemainder = count2 % EncryptorOutputBlockSize;
            int amountToEnc2 = count2 - secondRemainder;
            am = encryptor.TransformBlock(data2, offset2, amountToEnc2, output, outputOffset);
            amountEnc += am;
            outputOffset += am;


            var final = encryptor.TransformFinalBlock(data2, offset2 + amountToEnc2, secondRemainder);
            if (final.Length != 0)
            {
                Buffer.BlockCopy(final, 0, output, outputOffset, final.Length);
                amountEnc += final.Length;
            }

            return amountEnc;
        }


        public void Dispose()
        {
            encryptor.Dispose();
            decryptor.Dispose();
        }

        internal void ApplyIVEncryptor(byte[] iV)
        {
            encryptor.Dispose();
            encryptor = algorithm.CreateEncryptor(algorithm.Key, iV);
        }

        internal void ApplyIVDecryptor(byte[] iV)
        {
            decryptor.Dispose();
            decryptor = algorithm.CreateDecryptor(algorithm.Key, iV);
        }
    }
}
