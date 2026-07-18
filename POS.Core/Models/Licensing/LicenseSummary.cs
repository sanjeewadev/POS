using System;

namespace POS.Core.Models.Licensing
{
    public class LicenseSummary
    {
        // =========================================================
        // STORE LICENSE
        // =========================================================

        public string StoreId { get; set; } = string.Empty;

        public string StoreName { get; set; } = string.Empty;

        public string StoreLicenseId { get; set; } = string.Empty;

        public LicenseStatus StoreLicenseStatus { get; set; } = LicenseStatus.Missing;

        public DateTime? StoreExpiryDate { get; set; }

        public int StoreDaysRemaining { get; set; }

        // =========================================================
        // CURRENT MACHINE / TERMINAL
        // =========================================================

        public string CurrentMachineCode { get; set; } = string.Empty;

        public string CurrentMachineName { get; set; } = string.Empty;

        public string CurrentTerminalNo { get; set; } = string.Empty;

        public string CurrentTerminalName { get; set; } = string.Empty;

        // =========================================================
        // TERMINAL LICENSE
        // =========================================================

        public string TerminalLicenseId { get; set; } = string.Empty;

        public LicenseStatus TerminalLicenseStatus { get; set; } = LicenseStatus.Missing;

        public DateTime? TerminalExpiryDate { get; set; }

        public int TerminalDaysRemaining { get; set; }

        // =========================================================
        // OVERALL STATUS
        // =========================================================

        public LicenseStatus OverallStatus { get; set; } = LicenseStatus.Missing;

        public bool CanRunBackOffice { get; set; }

        public bool CanRunCashier { get; set; }

        public bool IsReadOnlyMode { get; set; }

        public string StatusMessage { get; set; } = string.Empty;

        public string StatusColor { get; set; } = "#64748B";

        public string StoreLicenseStatusText => StoreLicenseStatus.ToString();

        public string TerminalLicenseStatusText => TerminalLicenseStatus.ToString();

        public string OverallStatusText => OverallStatus.ToString();
    }
}