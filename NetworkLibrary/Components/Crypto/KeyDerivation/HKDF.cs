using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace NetworkLibrary.Components.Crypto.KeyDerivation
{
    public class HKDFLite
    {
        // HKDF implementation (RFC 5869)
        public static byte[] DeriveKey(string source, byte[] salt = null, byte[] info = null, int outputLength = 32)
        {
            if (string.IsNullOrEmpty(source))
            {
                source = "Saltmaker";
            }
            var bytes = Encoding.UTF8.GetBytes(source);
            if (salt == null)
            {
                salt = Encoding.UTF8.GetBytes("AES-GCM-Salt");
            }

            if (info == null)
            {
                info = Encoding.UTF8.GetBytes("AES-GCM-Info");
            }

            byte[] prk = HkdfExtract(salt, bytes);
            return HkdfExpand(prk, info, outputLength);
        }
        public static byte[] DeriveKey(byte[] sharedSecret, byte[] salt = null, byte[] info = null, int outputLength = 32)
        {
            if (salt == null)
            {
                salt = Encoding.UTF8.GetBytes("AES-GCM-Salt");
            }

            if (info == null)
            {
                info = Encoding.UTF8.GetBytes("AES-GCM-Info");
            }

            byte[] prk = HkdfExtract(salt, sharedSecret);
            return HkdfExpand(prk, info, outputLength);
        }

        private static byte[] HkdfExtract(byte[] salt, byte[] ikm)
        {
            using (var hmac = new HMACSHA256(salt))
            {
                return hmac.ComputeHash(ikm);
            }
        }

        private static byte[] HkdfExpand(byte[] prk, byte[] info, int outputLength)
        {
            using (var hmac = new HMACSHA256(prk))
            {
                byte[] result = new byte[outputLength];
                byte[] t = new byte[0];
                byte counter = 1;
                int offset = 0;

                while (offset < outputLength)
                {
                    // Concatenate T(i-1) + info + counter
                    byte[] input = new byte[t.Length + info.Length + 1];
                    Array.Copy(t, 0, input, 0, t.Length);
                    Array.Copy(info, 0, input, t.Length, info.Length);
                    input[t.Length + info.Length] = counter++;

                    // Compute T(i) = HMAC-Hash(PRK, T(i-1) | info | i)
                    t = hmac.ComputeHash(input);

                    // Copy to result
                    int toCopy = Math.Min(t.Length, outputLength - offset);
                    Array.Copy(t, 0, result, offset, toCopy);
                    offset += toCopy;
                }

                return result;
            }
        }
    }
}
