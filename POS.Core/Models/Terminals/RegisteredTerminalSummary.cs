using System;
using POS.Core.Models.Licensing;

namespace POS.Core.Models.Terminals
{
    public class RegisteredTerminalSummary
    {
        public int Id { get; set; }

        public string TerminalNo { get; set; } =
            string.Empty;

        public string TerminalName { get; set; } =
            string.Empty;

        public string MachineName { get; set; } =
            string.Empty;

        public string MachineCode { get; set; } =
            string.Empty;

        public bool IsCashierTerminal { get; set; }

        public bool IsActive { get; set; }

        public bool IsCurrentMachine { get; set; }

        public string LicenseId { get; set; } =
            string.Empty;

        public LicenseStatus LicenseStatus { get; set; } =
            LicenseStatus.Missing;

        public DateTime? LicenseExpiryDate { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public DateTime? LastLoginAt { get; set; }

        public DateTime? LastSaleAt { get; set; }

        public string UpdatedBy { get; set; } =
            string.Empty;

        public string ActiveStatusText =>
            IsActive ? "Active" : "Disabled";

        public string CurrentMachineText =>
            IsCurrentMachine ? "Yes" : "No";

        public string TerminalTypeText =>
            IsCashierTerminal
                ? "Cashier"
                : "BackOffice";

        public string LicenseStatusText =>
            IsCashierTerminal
                ? LicenseStatus switch
                {
                    LicenseStatus.Missing =>
                        "Missing",

                    LicenseStatus.Active =>
                        "Active",

                    LicenseStatus.ExpiringSoon =>
                        "Expiring Soon",

                    LicenseStatus.GracePeriod =>
                        "Grace Period",

                    LicenseStatus.ExpiredReadOnly =>
                        "Expired",

                    LicenseStatus.Invalid =>
                        "Invalid",

                    LicenseStatus.Revoked =>
                        "Revoked",

                    _ => "Unknown"
                }
                : "Not Required";

        public string LicenseExpiryDateText =>
            IsCashierTerminal &&
            LicenseExpiryDate.HasValue
                ? LicenseExpiryDate.Value
                    .ToString("yyyy-MM-dd")
                : "-";


        public DateTime? LastSeenAt =>
            LastSaleAt.HasValue && LastLoginAt.HasValue
                ? (LastSaleAt.Value >= LastLoginAt.Value
                    ? LastSaleAt
                    : LastLoginAt)
                : LastSaleAt ?? LastLoginAt;

        public string LastSeenText =>
            LastSeenAt.HasValue
                ? LastSeenAt.Value.ToString("yyyy-MM-dd HH:mm")
                : "-";

        public bool IsReady =>
            IsCashierTerminal &&
            IsActive &&
            HasMachineAssignment &&
            (this.LicenseStatus ==
                 POS.Core.Models.Licensing.LicenseStatus.Active ||
             this.LicenseStatus ==
                 POS.Core.Models.Licensing.LicenseStatus.ExpiringSoon);

        public bool RequiresAttention =>
            IsCashierTerminal &&
            IsActive &&
            !IsReady;

        public string SelectionText =>
            $"{TerminalNo} — {TerminalName} — {MachineNameDisplay}";

        public string AssignmentStatusText =>
            HasMachineAssignment
                ? "Assigned"
                : "Released";

        public string RegisteredAtText =>
            CreatedAt == default
                ? "-"
                : CreatedAt.ToString(
                    "yyyy-MM-dd HH:mm");

        public string UpdatedAtText =>
            UpdatedAt.HasValue
                ? UpdatedAt.Value.ToString(
                    "yyyy-MM-dd HH:mm")
                : "-";

        public bool HasMachineAssignment =>
            !string.IsNullOrWhiteSpace(MachineName) ||
            !string.IsNullOrWhiteSpace(MachineCode);

        public string MachineCodeDisplay =>
            string.IsNullOrWhiteSpace(
                MachineCode)
                ? "-"
                : MachineCode;

        public string MachineNameDisplay =>
            string.IsNullOrWhiteSpace(
                MachineName)
                ? "-"
                : MachineName;

        public string LicenseIdDisplay =>
            string.IsNullOrWhiteSpace(
                LicenseId)
                ? "-"
                : LicenseId;
    }
}
