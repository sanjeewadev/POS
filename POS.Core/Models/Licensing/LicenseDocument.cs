using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace POS.Core.Models.Licensing
{
    public class LicenseDocument
    {
        public int SchemaVersion { get; set; } = 1;

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

        // Kept for compatibility with the current database.
        // This simplified annual license has no after-expiry grace period.
        public int GraceDays { get; set; }

        public List<string> AllowedModules { get; set; } = new();

        public string Signature { get; set; } = string.Empty;

        [JsonIgnore]
        public bool IsStoreLicense =>
            LicenseType == LicenseType.StoreLicense;

        [JsonIgnore]
        public bool IsTerminalLicense =>
            LicenseType == LicenseType.TerminalLicense;
    }
}
