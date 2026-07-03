using System;

namespace POS.Core.Models.Licensing
{
    public class InstalledLicense
    {
        public int Id { get; set; }

        public string LicenseId { get; set; } = string.Empty;

        public LicenseType LicenseType { get; set; }

        public LicenseStatus LicenseStatus { get; set; } = LicenseStatus.Missing;

        public string StoreId { get; set; } = string.Empty;

        public string StoreName { get; set; } = string.Empty;

        // Empty for StoreLicense.
        // Required for TerminalLicense.
        public string TerminalNo { get; set; } = string.Empty;

        // Empty for StoreLicense.
        // Required for TerminalLicense.
        public string MachineCode { get; set; } = string.Empty;

        public DateTime IssuedOn { get; set; }

        public DateTime ExpiresOn { get; set; }

        public int GraceDays { get; set; } = 7;

        public DateTime ImportedAt { get; set; } = DateTime.Now;

        public string ImportedBy { get; set; } = string.Empty;

        public DateTime? LastVerifiedAt { get; set; }

        public string RawLicenseJson { get; set; } = string.Empty;

        public string Signature { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public string Remarks { get; set; } = string.Empty;
    }
}