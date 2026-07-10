using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using POS.Core.Models.Licensing;

namespace POS.Core.Services.Licensing
{
    public static class LicenseCanonicalPayloadBuilder
    {
        public static string Build(LicenseDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            var payload = new CanonicalPayload
            {
                SchemaVersion = document.SchemaVersion,
                KeyId = Normalize(document.KeyId),
                LicenseId = Normalize(document.LicenseId),
                LicenseType = document.LicenseType,
                StoreId = Normalize(document.StoreId),
                StoreName = Normalize(document.StoreName),
                TerminalNo = Normalize(document.TerminalNo),
                MachineCode =
                    Normalize(document.MachineCode).ToUpperInvariant(),
                IssuedOn = NormalizeDate(document.IssuedOn),
                ExpiresOn = NormalizeDate(document.ExpiresOn),
                GraceDays = document.GraceDays,
                AllowedModules = (document.AllowedModules ?? new())
                    .Where(module => !string.IsNullOrWhiteSpace(module))
                    .Select(Normalize)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(
                        module => module,
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            };

            return JsonSerializer.Serialize(
                payload,
                CreateJsonOptions());
        }

        private static JsonSerializerOptions CreateJsonOptions()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = null,
                DefaultIgnoreCondition =
                    JsonIgnoreCondition.Never
            };

            options.Converters.Add(
                new JsonStringEnumConverter());

            return options;
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

        private static string Normalize(string? value)
        {
            return (value ?? string.Empty).Trim();
        }

        private sealed class CanonicalPayload
        {
            public int SchemaVersion { get; set; }

            public string KeyId { get; set; } = string.Empty;

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

            public string[] AllowedModules { get; set; } =
                Array.Empty<string>();
        }
    }
}
