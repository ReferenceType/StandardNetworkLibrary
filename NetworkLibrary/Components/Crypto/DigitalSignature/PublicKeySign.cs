using NetworkLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace NetworkLibrary.Components.Crypto.DigitalSignature
{
    internal class PublicKeySign
    {

        private static readonly ECCurve curve = ECCurve.NamedCurves.nistP256;

        public static (byte[] privateKey, byte[] publicKey) GenerateKeys()
        {
            using (var ecdsa = ECDsa.Create(curve))
            {
                var parameters = ecdsa.ExportParameters(true);
                return (
                    privateKey: parameters.D,
                    publicKey: CombineBytes(parameters.Q.X, parameters.Q.Y)
                );
            }
        }

        public static byte[] SignData(byte[] data,int offset,int count, byte[] privateKey)
        {
            using (var ecdsa = ECDsa.Create(curve))
            {
                ecdsa.ImportParameters(new ECParameters
                {
                    Curve = curve,
                    D = privateKey,
                    Q = DerivePublicKey(privateKey)
                });
                return ecdsa.SignData(data,offset,count, HashAlgorithmName.SHA256);
            }
        }

        public static byte[] SignData(byte[] data, int offset, int count, ECParameters privateKeyParams)
        {
            using (var ecdsa = ECDsa.Create(curve))
            {
                ecdsa.ImportParameters(privateKeyParams);
                return ecdsa.SignData(data, offset, count, HashAlgorithmName.SHA256);
            }

        }


        public static bool VerifyData(byte[] data,int offset,int count, byte[] signature, byte[] publicKey)
        {
            using (var ecdsa = ECDsa.Create(curve))
            {
                if (publicKey.Length != 64)
                    throw new ArgumentException("Public key must be 64 bytes (X+Y concatenated)");

                ecdsa.ImportParameters(new ECParameters
                {
                    Curve = curve,
                    Q = new ECPoint
                    {
                        X = ByteCopy.ToArray(publicKey, 0, 32),//publicKey[0..32],
                        Y = ByteCopy.ToArray(publicKey, 32, 32) //publicKey[32..64]
                    }
                });
                return ecdsa.VerifyData(data,offset,count, signature, HashAlgorithmName.SHA256);
            }
        }

        public static bool VerifyData(byte[] data, int offset, int count, byte[] signature, ECParameters publicKeyParams)
        {
            using (var ecdsa = ECDsa.Create(curve))
            {
                ecdsa.ImportParameters(publicKeyParams);
                return ecdsa.VerifyData(data, offset, count, signature, HashAlgorithmName.SHA256);
            }
        }

        private static ECPoint DerivePublicKey(byte[] privateKey)
        {
            using (var ecdsa = ECDsa.Create(curve))
            {
                ecdsa.ImportParameters(new ECParameters
                {
                    Curve = curve,
                    D = privateKey
                });
                return ecdsa.ExportParameters(false).Q;
            }
        }

        private static byte[] CombineBytes(byte[] first, byte[] second)
        {
            var result = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, result, 0, first.Length);
            Buffer.BlockCopy(second, 0, result, first.Length, second.Length);
            return result;
        }

    }
}
