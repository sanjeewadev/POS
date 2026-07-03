using System;
using POS.Core.Models.Licensing;

namespace POS.Core.Models.Terminals
{
    public class RegisteredTerminalSummary
    {
        public int Id { get; set; }

        public string TerminalNo { get; set; } = string.Empty;

        public string TerminalName { get; set; } = string.Empty;

        public string MachineName { get; set; } = string.Empty;

        public string MachineCode { get; set; } = string.Empty;

        public string Location { get; set; } = string.Empty;

        public bool IsCashierTerminal { get; set; }

        public bool IsBackOfficeAllowed { get; set; }

        public bool IsActive { get; set; }

        public string LicenseId { get; set; } = string.Empty;

        public LicenseStatus LicenseStatus { get; set; } = LicenseStatus.Missing;

        public DateTime? LicenseExpiryDate { get; set; }

        public DateTime? LastLoginAt { get; set; }

        public DateTime? LastSaleAt { get; set; }

        public string TerminalTypeText => IsCashierTerminal
            ? "Cashier Terminal"
            : "BackOffice Only";

        public string ActiveStatusText => IsActive
            ? "Active"
            : "Inactive";

        public string LicenseStatusText => LicenseStatus switch
        {
            LicenseStatus.Missing => "Missing",
            LicenseStatus.Active => "Active",
            LicenseStatus.ExpiringSoon => "Expiring Soon",
            LicenseStatus.GracePeriod => "Grace Period",
            LicenseStatus.ExpiredReadOnly => "Expired / Read Only",
            LicenseStatus.Invalid => "Invalid",
            LicenseStatus.Revoked => "Revoked",
            _ => "Unknown"
        };

        public string LicenseExpiryDateText => LicenseExpiryDate.HasValue
            ? LicenseExpiryDate.Value.ToString("yyyy-MM-dd")
            : "-";

        public string LastLoginText => LastLoginAt.HasValue
            ? LastLoginAt.Value.ToString("yyyy-MM-dd HH:mm")
            : "-";

        public string LastSaleText => LastSaleAt.HasValue
            ? LastSaleAt.Value.ToString("yyyy-MM-dd HH:mm")
            : "-";

        public string MachineCodeDisplay => string.IsNullOrWhiteSpace(MachineCode)
            ? "-"
            : MachineCode;

        public string LicenseIdDisplay => string.IsNullOrWhiteSpace(LicenseId)
            ? "-"
            : LicenseId;
    }
}