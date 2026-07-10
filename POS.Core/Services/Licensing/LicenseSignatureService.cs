using System;
using System.Security.Cryptography;
using System.Text;
using POS.Core.Models.Licensing;

namespace POS.Core.Services.Licensing
{
    public class LicenseSignatureService
    {
        public bool VerifySignature(
            LicenseDocument document)
        {
            if (document == null ||
                string.IsNullOrWhiteSpace(
                    document.Signature) ||
                !IsPublicKeyConfigured())
            {
                return false;
            }

            if (document.SchemaVersion !=
                LicensePolicy.CurrentSchemaVersion)
            {
                return false;
            }

            if (!string.Equals(
                    document.KeyId,
                    LicensePublicKey.KeyId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                string payload =
                    LicenseCanonicalPayloadBuilder
                        .Build(document);

                byte[] payloadBytes =
                    Encoding.UTF8.GetBytes(payload);

                byte[] signatureBytes =
                    Convert.FromBase64String(
                        document.Signature);

                using RSA rsa = RSA.Create();
                rsa.ImportFromPem(
                    LicensePublicKey.PublicKeyPem);

                return rsa.VerifyData(
                    payloadBytes,
                    signatureBytes,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);
            }
            catch
            {
                return false;
            }
        }

        public bool IsPublicKeyConfigured()
        {
            return !string.IsNullOrWhiteSpace(
                       LicensePublicKey.KeyId) &&
                   !string.IsNullOrWhiteSpace(
                       LicensePublicKey.PublicKeyPem) &&
                   LicensePublicKey.PublicKeyPem.Contains(
                       "BEGIN PUBLIC KEY",
                       StringComparison.Ordinal);
        }

        public string GetConfiguredKeyId()
        {
            return LicensePublicKey.KeyId;
        }
    }
}
