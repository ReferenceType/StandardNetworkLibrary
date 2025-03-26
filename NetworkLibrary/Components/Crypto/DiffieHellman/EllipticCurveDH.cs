using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace NetworkLibrary.Components.Crypto.DiffieHellman
{
#if NET6_0_OR_GREATER
    public class ECDH : IDisposable
    {
        private readonly ECDiffieHellman _ecdh;

        public ECDH()
        {
            _ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        }

        public ECDH(ECCurve curve)
        {
            _ecdh = ECDiffieHellman.Create(curve);
        }

        public byte[] GetPublicKey()
        {
            return _ecdh.PublicKey.ExportSubjectPublicKeyInfo();
        }

        public byte[] CalculateSharedSecret(byte[] otherPublicKeyBytes)
        {
            using (ECDiffieHellman otherParty = ECDiffieHellman.Create())
            {
                otherParty.ImportSubjectPublicKeyInfo(otherPublicKeyBytes, out _);
                return _ecdh.DeriveKeyMaterial(otherParty.PublicKey);
            }
        }

        public void Dispose()
        {
            _ecdh.Dispose();
        }
    }
#endif
}
