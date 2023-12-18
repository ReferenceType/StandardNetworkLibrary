using NetworkLibrary.Components.Crypto;
using NetworkLibrary.Components.Crypto.Algorithms;
using NetworkLibrary.Utils;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Drawing;
using System.Security.Cryptography;

namespace NetworkLibrary.Components
{
    public class ConcurrentAesAlgorithm
    {
        IAesEngine encryptionEngine;
        IAesEngine cbcRandomIV;
        IAesEngine cbcCounterIV;
        IAesEngine cbcCounterIVHMAC;
        IAesEngine gcm;
        IAesEngine noEncryption;

        public ConcurrentAesAlgorithm(byte[] key, AesMode aesType = AesMode.GCM)
        {
            var IV = GenerateIV(key);
            encryptionEngine = new AesManager(key, IV, aesType);
            cbcRandomIV = new AesManager(key, IV, AesMode.CBCRandomIV);
            cbcCounterIV = new AesManager(key, IV, AesMode.CBCCtrIV);
            cbcCounterIVHMAC = new AesManager(key, IV, AesMode.CBCCtrIVHMAC);

            gcm = new AesManager(key, IV, AesMode.GCM);
            noEncryption = new AesManager(key, IV, AesMode.None);
        }

        private byte[] GenerateIV(byte[] key)
        {
            SHA256 sHA = SHA256.Create();
            var hash = sHA.ComputeHash(key);
            sHA.Dispose();
            return ByteCopy.ToArray(hash, 0, 16);
        }

        private IAesEngine GetEngine(byte val)
        {
            switch (val)
            {
                case (byte)AesMode.CBCRandomIV:
                    return cbcRandomIV;
                case (byte)AesMode.CBCCtrIV:
                    return cbcCounterIV;
                case (byte)AesMode.CBCCtrIVHMAC:
                    return cbcCounterIVHMAC;
                case (byte)AesMode.GCM:
                    return gcm;
                case (byte)AesMode.None:
                    return noEncryption;
               
                default:
                    throw new NotImplementedException(val.ToString()+" - type of encryption algorithm is not implemented");
            }
        }
        public byte[] Decrypt(byte[] bytes) => Decrypt(bytes, 0, bytes.Length);
        public byte[] Decrypt(byte[] bytes, int offset, int count)
        {
            byte val = bytes[offset++];
            count--;
            return GetEngine(val).Decrypt(bytes, offset, count);
           
        }
        public int DecryptInto(byte[] source, int sourceOffset, int sourceCount, byte[] output, int outputOffset)
        {
            byte val = source[sourceOffset++];
            sourceCount--;
            return GetEngine(val).DecryptInto(source, sourceOffset, sourceCount, output, outputOffset); 
        }

        public byte[] Encrypt(byte[] bytes) => Encrypt(bytes, 0, bytes.Length);
        public byte[] Encrypt(byte[] bytes, int offset, int count)
        {
            return encryptionEngine.Encrypt(bytes, offset, count);
        }

        public int EncryptInto(byte[] source, int sourceOffset, int sourceCount, byte[] output, int outputOffset)
        {
            return encryptionEngine.EncryptInto(source,sourceOffset,sourceCount,output,outputOffset);
        }

        public int EncryptInto(byte[] data1, int offset1, int count1, byte[] data2, int offset2, int count2, byte[] output, int outputOffset)
        {
            return encryptionEngine.EncryptInto(data1, offset1, count1, data2, offset2, count2, output, outputOffset);
        }
    }
}

