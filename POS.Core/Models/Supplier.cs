using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class Supplier
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string SupplierCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string SupplierName { get; set; } = string.Empty;

        [MaxLength(150)]
        public string CompanyName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string ContactPerson { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Phone1 { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Phone2 { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(250)]
        public string Address { get; set; } = string.Empty;

        // =========================================================
        // FINANCIAL / TAX SETUP
        // =========================================================

        // Final supplier VAT rule:
        // If false, PO/GRN must force VAT to zero for this supplier.
        // Kept as HasVat because existing code and database already use this column.
        public bool HasVat { get; set; } = false;

        [MaxLength(50)]
        public string VatNumber { get; set; } = string.Empty;

        // Alias for clearer business meaning in future code.
        // Not mapped because HasVat remains the real stored field.
        [NotMapped]
        public bool IsVatRegistered
        {
            get => HasVat;
            set => HasVat = value;
        }

        [NotMapped]
        public string VatStatusText => HasVat ? "VAT Registered" : "Not VAT Registered";

        [NotMapped]
        public string VatDisplayText
        {
            get
            {
                if (!HasVat)
                    return "No VAT";

                return string.IsNullOrWhiteSpace(VatNumber)
                    ? "VAT Registered"
                    : $"VAT Registered - {VatNumber.Trim()}";
            }
        }

        // Used by PO / GRN payment due date calculation.
        // Example:
        // Supplier invoice date: 2026-06-27
        // DefaultCreditDays: 30
        // Due date: 2026-07-27
        public int DefaultCreditDays { get; set; } = 30;

        // Driven by accounting/ledger.
        // Supplier Master should display this, but should not directly update it.
        [Column(TypeName = "decimal(18,2)")]
        public decimal CurrentBalance { get; set; } = 0m;

        // Used instead of hard delete when supplier is already linked to documents.
        public bool IsDeactivated { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public DateTime? DeactivatedAt { get; set; }

        [NotMapped]
        public string StatusText => IsDeactivated ? "Suspended" : "Active";

        [NotMapped]
        public string DisplayName
        {
            get
            {
                string code = (SupplierCode ?? string.Empty).Trim();
                string name = (SupplierName ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(code))
                    return name;

                if (string.IsNullOrWhiteSpace(name))
                    return code;

                return $"{code} - {name}";
            }
        }

        // Items supplied by this vendor.
        public virtual ICollection<ItemSupplier> ItemSuppliers { get; set; } = new List<ItemSupplier>();
    }
}
