using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using POS.Core.Models.Licensing;

namespace POS.Core.Services.Licensing
{
    public class LicenseSignatureService
    {
        /*
            IMPORTANT:
            Replace this public key before production.

            Your private License Generator tool will hold the PRIVATE key.
            Customer POS app must only contain this PUBLIC key.

            Current placeholder will compile, but real license verification
            will fail until this is replaced with your real public key.
        */
        private const string PublicKeyPem = """
-----BEGIN PUBLIC KEY-----
REPLACE_THIS_WITH_YOUR_REAL_RSA_PUBLIC_KEY
-----END PUBLIC KEY-----
""";

        public bool VerifySignature(LicenseDocument document)
        {
            if (document == null)
                return false;

            if (string.IsNullOrWhiteSpace(document.Signature))
                return false;

            if (!IsPublicKeyConfigured())
                return false;

            try
            {
                string payload = BuildPayloadForSigning(document);
                byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
                byte[] signatureBytes = Convert.FromBase64String(document.Signature);

                using RSA rsa = RSA.Create();
                rsa.ImportFromPem(PublicKeyPem);

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

        public string BuildPayloadForSigning(LicenseDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            var payload = new LicenseSigningPayload
            {
                LicenseId = Normalize(document.LicenseId),
                LicenseType = document.LicenseType,
                StoreId = Normalize(document.StoreId),
                StoreName = Normalize(document.StoreName),
                TerminalNo = Normalize(document.TerminalNo),
                MachineCode = Normalize(document.MachineCode).ToUpperInvariant(),
                IssuedOn = document.IssuedOn.Date,
                ExpiresOn = document.ExpiresOn.Date,
                GraceDays = document.GraceDays,
                AllowedModules = document.AllowedModules
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Select(m => Normalize(m))
                    .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            };

            return JsonSerializer.Serialize(payload, CreateCanonicalJsonOptions());
        }

        public bool IsPublicKeyConfigured()
        {
            return !string.IsNullOrWhiteSpace(PublicKeyPem)
                   && !PublicKeyPem.Contains("REPLACE_THIS_WITH_YOUR_REAL_RSA_PUBLIC_KEY", StringComparison.OrdinalIgnoreCase);
        }

        private static JsonSerializerOptions CreateCanonicalJsonOptions()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = null,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never
            };

            options.Converters.Add(new JsonStringEnumConverter());

            return options;
        }

        private static string Normalize(string? value)
        {
            return (value ?? string.Empty).Trim();
        }

        private sealed class LicenseSigningPayload
        {
            public string LicenseId { get; set; } = string.Empty;

            [JsonConverter(typeof(JsonStringEnumConverter))]
            public LicenseType LicenseType { get; set; }

            public string StoreId { get; set; } = string.Empty;

            public string StoreName { get; set; } = string.Empty;

            public string TerminalNo { get; set; } = string.Empty;

            public string MachineCode { get; set; } = string.Empty;

            public DateTime IssuedOn { get; set; }

            public DateTime ExpiresOn { get; set; }

            public int GraceDays { get; set; }

            public string[] AllowedModules { get; set; } = Array.Empty<string>();
        }
    }
}