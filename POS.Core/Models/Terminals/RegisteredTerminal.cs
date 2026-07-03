using System;

namespace POS.Core.Models.Terminals
{
    public class RegisteredTerminal
    {
        public int Id { get; set; }

        // =========================================================
        // TERMINAL IDENTITY
        // =========================================================

        public string TerminalNo { get; set; } = string.Empty;

        public string TerminalName { get; set; } = string.Empty;

        public string MachineName { get; set; } = string.Empty;

        public string MachineCode { get; set; } = string.Empty;

        public string Location { get; set; } = "Main Store";

        // =========================================================
        // TERMINAL TYPE / ACCESS
        // =========================================================

        // True = cashier/selling terminal and needs terminal license.
        // False = admin/backoffice-only machine and does not need terminal license.
        public bool IsCashierTerminal { get; set; } = true;

        public bool IsBackOfficeAllowed { get; set; } = true;

        public bool IsActive { get; set; } = true;

        // =========================================================
        // LICENSE SNAPSHOT
        // =========================================================

        public string LicenseId { get; set; } = string.Empty;

        public DateTime? LicenseExpiryDate { get; set; }

        public DateTime? LicenseLastCheckedAt { get; set; }

        // =========================================================
        // ACTIVITY
        // =========================================================

        public DateTime? LastLoginAt { get; set; }

        public DateTime? LastSaleAt { get; set; }

        // =========================================================
        // AUDIT
        // =========================================================

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        public string UpdatedBy { get; set; } = string.Empty;

        public string Remarks { get; set; } = string.Empty;
    }
}