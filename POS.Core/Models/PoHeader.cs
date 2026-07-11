using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using POS.Core.Configuration;

namespace POS.Core.Models
{
    public class PoHeader
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string PoNumber { get; set; } = string.Empty;

        [Required]
        public int SupplierId { get; set; }

        public Supplier Supplier { get; set; } = null!;

        public DateTime OrderDate { get; set; } = DateTime.Now;

        public DateTime ExpectedDate { get; set; } = DateTime.Now.AddDays(7);

        [MaxLength(50)]
        public string Terms { get; set; } = "Credit";

        public int CreditDays { get; set; } = 30;

        [MaxLength(500)]
        public string Remarks { get; set; } = string.Empty;

        // =========================================================
        // FINANCIAL TOTALS
        // =========================================================

        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal GlobalBillDiscount { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalTaxAmount { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalDiscountAmount { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetPayable { get; set; } = 0m;

        // =========================================================
        // IMMUTABLE TAX SNAPSHOT FOUNDATION
        // =========================================================
        // Nullable totals preserve the fact that historical documents
        // were created before authoritative VAT snapshots existed.

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TaxableAmountTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? StandardRatedAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? ZeroRatedAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? ExemptAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? OutOfScopeAmount { get; set; }

        [Required]
        [MaxLength(30)]
        public string TaxSnapshotStatus { get; set; } = TaxSnapshotStatuses.LegacyUnknown;

        // False = tax added on top.
        // True = prices already include tax.
        public bool IsTaxInclusive { get; set; } = false;

        // =========================================================
        // DOCUMENT STATUS
        // =========================================================
        // Normal lifecycle:
        // Draft -> Approved -> Partially Received -> Closed
        // Cancelled can happen before Closed.

        [MaxLength(30)]
        public string Status { get; set; } = "Draft";

        [MaxLength(50)]
        public string CreatedBy { get; set; } = string.Empty;

        [MaxLength(50)]
        public string ApprovedBy { get; set; } = string.Empty;

        [MaxLength(50)]
        public string CancelledBy { get; set; } = string.Empty;

        [MaxLength(250)]
        public string CancellationReason { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public DateTime? ApprovedAt { get; set; }

        public DateTime? CancelledAt { get; set; }

        public DateTime? ClosedAt { get; set; }

        public ICollection<PoLine> PoLines { get; set; } = new List<PoLine>();

        [NotMapped]
        public bool IsEditable =>
            Status == "Draft" || Status == "Approved";

        [NotMapped]
        public bool IsReceivable =>
            Status == "Approved" || Status == "Partially Received";
    }
}