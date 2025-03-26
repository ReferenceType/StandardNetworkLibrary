using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace NetworkLibrary.Components.Crypto.DigitalSignature
{
    public class PrivateKeySign
    {
        HMACSHA256 sha256;
        public PrivateKeySign(byte[] key)
        {
            sha256 = new HMACSHA256(key);
        }

        public byte[] Sign(byte[] data)
        {
           return sha256.ComputeHash(data);
        }

        public byte[] Sign(byte[] buffer, int offset, int count) 
        {
            return sha256.ComputeHash(buffer, offset, count);
        }

    }
}
