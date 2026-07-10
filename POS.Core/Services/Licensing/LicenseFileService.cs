using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using POS.Core.Models.Licensing;

namespace POS.Core.Services.Licensing
{
    public class LicenseFileService
    {
        private const long MaximumLicenseFileSizeBytes =
            256 * 1024;

        public async Task<LicenseDocument>
            ReadLicenseFileAsync(string filePath)
        {
            var result =
                await ReadLicenseFileWithRawJsonAsync(
                    filePath);

            return result.Document;
        }

        public async Task<
            (LicenseDocument Document, string RawJson)>
            ReadLicenseFileWithRawJsonAsync(
                string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new InvalidOperationException(
                    "License file path is required.");
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    "License file was not found.",
                    filePath);
            }

            if (!string.Equals(
                    Path.GetExtension(filePath),
                    ".poslic",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Invalid license file type. Select a .poslic file.");
            }

            var fileInfo = new FileInfo(filePath);

            if (fileInfo.Length <= 0)
            {
                throw new InvalidOperationException(
                    "License file is empty.");
            }

            if (fileInfo.Length >
                MaximumLicenseFileSizeBytes)
            {
                throw new InvalidOperationException(
                    "License file is too large.");
            }

            string rawJson =
                await File.ReadAllTextAsync(filePath);

            LicenseDocument document =
                ReadLicenseJson(rawJson);

            return (document, rawJson);
        }

        public LicenseDocument ReadLicenseJson(
            string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                throw new InvalidOperationException(
                    "License file is empty.");
            }

            LicenseDocument? document;

            try
            {
                document =
                    JsonSerializer.Deserialize<
                        LicenseDocument>(
                        rawJson,
                        CreateJsonOptions());
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Invalid license file format: {ex.Message}");
            }

            if (document == null)
            {
                throw new InvalidOperationException(
                    "License data could not be read.");
            }

            Normalize(document);
            ValidateBasic(document);

            return document;
        }

        public string SerializeLicenseDocument(
            LicenseDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(
                    nameof(document));

            Normalize(document);
            ValidateBasic(document);

            return JsonSerializer.Serialize(
                document,
                CreateJsonOptions());
        }

        private static JsonSerializerOptions
            CreateJsonOptions()
        {
            var options =
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNameCaseInsensitive = false
                };

            options.Converters.Add(
                new JsonStringEnumConverter());

            return options;
        }

        private static void Normalize(
            LicenseDocument document)
        {
            document.KeyId =
                NormalizeText(document.KeyId);

            document.LicenseId =
                NormalizeText(document.LicenseId);

            document.StoreId =
                NormalizeText(document.StoreId);

            document.StoreName =
                NormalizeText(document.StoreName);

            document.TerminalNo =
                NormalizeText(document.TerminalNo);

            document.MachineCode =
                NormalizeText(document.MachineCode)
                    .ToUpperInvariant();

            document.Signature =
                NormalizeText(document.Signature);

            document.GraceDays = 0;

            document.IssuedOn =
                NormalizeDate(document.IssuedOn);

            document.ExpiresOn =
                NormalizeDate(document.ExpiresOn);

            document.AllowedModules ??= new();

            for (int index =
                     document.AllowedModules.Count - 1;
                 index >= 0;
                 index--)
            {
                document.AllowedModules[index] =
                    NormalizeText(
                        document.AllowedModules[index]);

                if (string.IsNullOrWhiteSpace(
                        document.AllowedModules[index]))
                {
                    document.AllowedModules
                        .RemoveAt(index);
                }
            }
        }

        private static void ValidateBasic(
            LicenseDocument document)
        {
            if (document.SchemaVersion !=
                LicensePolicy.CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    $"Unsupported license format version " +
                    $"'{document.SchemaVersion}'.");
            }

            if (string.IsNullOrWhiteSpace(
                    document.KeyId))
            {
                throw new InvalidOperationException(
                    "License key ID is missing.");
            }

            if (string.IsNullOrWhiteSpace(
                    document.LicenseId))
            {
                throw new InvalidOperationException(
                    "License ID is required.");
            }

            if (document.LicenseType !=
                    LicenseType.StoreLicense &&
                document.LicenseType !=
                    LicenseType.TerminalLicense)
            {
                throw new InvalidOperationException(
                    "Invalid license type.");
            }

            if (string.IsNullOrWhiteSpace(
                    document.StoreId))
            {
                throw new InvalidOperationException(
                    "Store ID is required.");
            }

            if (string.IsNullOrWhiteSpace(
                    document.StoreName))
            {
                throw new InvalidOperationException(
                    "Store name is required.");
            }

            if (document.IssuedOn == default)
            {
                throw new InvalidOperationException(
                    "Issued date is required.");
            }

            if (document.ExpiresOn == default)
            {
                throw new InvalidOperationException(
                    "Expiry date is required.");
            }

            if (document.ExpiresOn.Date <
                document.IssuedOn.Date)
            {
                throw new InvalidOperationException(
                    "Expiry date cannot be earlier than issued date.");
            }

            if (document.IsTerminalLicense)
            {
                if (string.IsNullOrWhiteSpace(
                        document.TerminalNo))
                {
                    throw new InvalidOperationException(
                        "Terminal number is required.");
                }

                if (string.IsNullOrWhiteSpace(
                        document.MachineCode))
                {
                    throw new InvalidOperationException(
                        "Machine code is required.");
                }
            }

            if (string.IsNullOrWhiteSpace(
                    document.Signature))
            {
                throw new InvalidOperationException(
                    "License signature is missing.");
            }
        }

        private static DateTime NormalizeDate(
            DateTime value)
        {
            return value == default
                ? value
                : DateTime.SpecifyKind(
                    value.Date,
                    DateTimeKind.Unspecified);
        }

        private static string NormalizeText(
            string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}
