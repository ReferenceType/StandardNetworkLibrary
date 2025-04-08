using System;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace NetworkLibrary.Components.Crypto.DiffieHellman
{
    public class DiffieHellman
    {
        // 2048-bit MODP group from RFC 3526
        private const string PrimeHex = "0FFFFFFFFFFFFFFFFC90FDAA22168C234C4C6628B80DC1CD129024E088A67CC74020BB" +
            "EA63B139B22514A08798E3404DDEF9519B3CD3A431B302B0A6DF25F14374FE1356D6D51C245E485B576625E7EC6F44C42E" +
            "9A637ED6B0BFF5CB6F406B7EDEE386BFB5A899FA5AE9F24117C4B1FE649286651ECE45B3DC2007CB8A163BF0598DA48361" +
            "C55D39A69163FA8FD24CF5F83655D23DCA3AD961C62F356208552BB9ED529077096966D670C354E4ABC9804F1746C08CA1" +
            "8217C32905E462E36CE3BE39E772C180E86039B2783A2EC07A28FB5C55DF06F4C52C9DE2BCBF6955817183995497CEA956" +
            "AE515D2261898FA051015728E5A8AACAA68FFFFFFFFFFFFFFFF";

        private static readonly BigInteger _prime = BigInteger.Parse(PrimeHex, System.Globalization.NumberStyles.HexNumber);

        private static readonly BigInteger Generator = new BigInteger(2); // 2 is also from RFC 3526

        private readonly BigInteger _privateKey;
        private readonly BigInteger _publicKey;
        public DiffieHellman()
        {
            _privateKey = GenerateRandomPrivateKey();
            _publicKey = BigInteger.ModPow(Generator, _privateKey, _prime);
        }

        public byte[] GetPublicKey()
        {
            return _publicKey.ToByteArray();
        }

        public byte[] CalculateSharedSecret(byte[] otherPublicKeyBytes)
        {
            BigInteger otherPublicKey = new BigInteger(otherPublicKeyBytes);
            BigInteger sharedSecret = BigInteger.ModPow(otherPublicKey, _privateKey, _prime);
            return sharedSecret.ToByteArray();
        }

       

        private BigInteger GenerateRandomPrivateKey()
        {
            // Recommended key size for security (at least 256 bits)
            int keySize = 256 / 8;
            byte[] randomBytes = new byte[keySize + 1]; // Extra byte to ensure positive BigInteger

            using (var rng = RandomNumberGenerator.Create())
            {
                do
                {
                    rng.GetBytes(randomBytes);
                    // Ensure private key is in [1, p-1]
                    var key = new BigInteger(randomBytes);
                    if (key > 1 && key < _prime - 1)
                        return key;
                } while (true);
            }
        }

    }
}
