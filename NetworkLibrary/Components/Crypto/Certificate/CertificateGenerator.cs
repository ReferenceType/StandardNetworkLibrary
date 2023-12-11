using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NetworkLibrary.Components.Crypto.Certificate.Native;

namespace NetworkLibrary.Components.Crypto.Certificate

{
    public static class CertificateGenerator
    {
        public static X509Certificate2 GenerateSelfSignedCertificate()
        {

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
            return GenerateSelfSignedDotNet();
#else
            return GenerateSelfSignedNative();
#endif

        }

        private static X509Certificate2 GenerateSelfSignedNative()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                throw new PlatformNotSupportedException("Certificate generation on .Net standard 2.0 is only supported in windows");

            X509Certificate2 cert;
            using (CryptContext ctx = new CryptContext())
            {
                ctx.Open();

                cert = ctx.CreateSelfSignedCertificate(
                    new SelfSignedCertProperties
                    {
                        IsPrivateKeyExportable = true,
                        KeyBitLength = 2048,
                        Name = new X500DistinguishedName("cn=localhost"),
                        ValidFrom = DateTime.Today.AddDays(-1),
                        ValidTo = DateTime.Today.AddYears(1),
                    });


            }
            return new X509Certificate2(cert.Export(X509ContentType.Pfx));

        }
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_1_OR_GREATER
        private static X509Certificate2 GenerateSelfSignedDotNet()
        {
            using (RSA parent = RSA.Create(2048))
            {
                CertificateRequest parentReq = new CertificateRequest(
                    "CN=localhost",
                    parent,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);

                parentReq.CertificateExtensions.Add(
                    new X509BasicConstraintsExtension(true, false, 0, true));

                parentReq.CertificateExtensions.Add(
                    new X509SubjectKeyIdentifierExtension(parentReq.PublicKey, false));

                X509Certificate2 parentCert = parentReq.CreateSelfSigned(
                    DateTimeOffset.UtcNow.AddDays(-45),
                    DateTimeOffset.UtcNow.AddDays(365));
                return new X509Certificate2(parentCert.Export(X509ContentType.Pfx));
            }
        }

#endif

    }
}



