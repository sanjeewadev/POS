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
        public async Task<LicenseDocument> ReadLicenseFileAsync(string filePath)
        {
            var result = await ReadLicenseFileWithRawJsonAsync(filePath);
            return result.Document;
        }

        public async Task<(LicenseDocument Document, string RawJson)> ReadLicenseFileWithRawJsonAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new InvalidOperationException("License file path is required.");

            if (!File.Exists(filePath))
                throw new FileNotFoundException("License file was not found.", filePath);

            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            if (extension != ".dat" && extension != ".lic" && extension != ".json")
                throw new InvalidOperationException("Invalid license file type. Please select a .dat, .lic, or .json license file.");

            string rawJson = await File.ReadAllTextAsync(filePath);

            LicenseDocument document = ReadLicenseJson(rawJson);

            return (document, rawJson);
        }

        public LicenseDocument ReadLicenseJson(string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
                throw new InvalidOperationException("License file is empty.");

            LicenseDocument? document;

            try
            {
                document = JsonSerializer.Deserialize<LicenseDocument>(
                    rawJson,
                    CreateJsonOptions());
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Invalid license file format: {ex.Message}");
            }

            if (document == null)
                throw new InvalidOperationException("Invalid license file. License data could not be read.");

            Normalize(document);
            ValidateBasic(document);

            return document;
        }

        public string SerializeLicenseDocument(LicenseDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            Normalize(document);
            ValidateBasic(document);

            return JsonSerializer.Serialize(document, CreateJsonOptions());
        }

        private static void Normalize(LicenseDocument document)
        {
            document.LicenseId = NormalizeText(document.LicenseId);
            document.StoreId = NormalizeText(document.StoreId);
            document.StoreName = NormalizeText(document.StoreName);
            document.TerminalNo = NormalizeText(document.TerminalNo);
            document.MachineCode = NormalizeText(document.MachineCode).ToUpperInvariant();
            document.Signature = NormalizeText(document.Signature);

            if (document.GraceDays < 0)
                document.GraceDays = 0;

            document.IssuedOn = document.IssuedOn.Date;
            document.ExpiresOn = document.ExpiresOn.Date;

            for (int i = document.AllowedModules.Count - 1; i >= 0; i--)
            {
                document.AllowedModules[i] = NormalizeText(document.AllowedModules[i]);

                if (string.IsNullOrWhiteSpace(document.AllowedModules[i]))
                    document.AllowedModules.RemoveAt(i);
            }
        }

        private static void ValidateBasic(LicenseDocument document)
        {
            if (string.IsNullOrWhiteSpace(document.LicenseId))
                throw new InvalidOperationException("License ID is required.");

            if (document.LicenseType != LicenseType.StoreLicense &&
                document.LicenseType != LicenseType.TerminalLicense)
                throw new InvalidOperationException("Invalid license type.");

            if (string.IsNullOrWhiteSpace(document.StoreId))
                throw new InvalidOperationException("Store ID is required.");

            if (string.IsNullOrWhiteSpace(document.StoreName))
                throw new InvalidOperationException("Store name is required.");

            if (document.IssuedOn == default)
                throw new InvalidOperationException("License issued date is required.");

            if (document.ExpiresOn == default)
                throw new InvalidOperationException("License expiry date is required.");

            if (document.ExpiresOn < document.IssuedOn)
                throw new InvalidOperationException("License expiry date cannot be earlier than issued date.");

            if (document.LicenseType == LicenseType.TerminalLicense)
            {
                if (string.IsNullOrWhiteSpace(document.TerminalNo))
                    throw new InvalidOperationException("Terminal number is required for terminal license.");

                if (string.IsNullOrWhiteSpace(document.MachineCode))
                    throw new InvalidOperationException("Machine code is required for terminal license.");
            }

            if (string.IsNullOrWhiteSpace(document.Signature))
                throw new InvalidOperationException("License signature is missing.");
        }

        private static JsonSerializerOptions CreateJsonOptions()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            options.Converters.Add(new JsonStringEnumConverter());

            return options;
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}