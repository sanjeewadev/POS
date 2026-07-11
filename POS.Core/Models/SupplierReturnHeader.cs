using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class SupplierReturnHeader
    {
        [Key]
        public int Id { get; set; }

        // Example: RTN-00001.
        [Required]
        [MaxLength(30)]
        public string ReturnNumber { get; set; } = string.Empty;

        // =========================================================
        // SUPPLIER / SOURCE DOCUMENT
        // =========================================================

        [Required]
        public int SupplierId { get; set; }

        [ForeignKey(nameof(SupplierId))]
        public virtual Supplier Supplier { get; set; } = null!;

        // Business rule:
        // Supplier Return should be GRN-based for now.
        // Kept nullable for database compatibility, but repository must validate it.
        public int? GrnHeaderId { get; set; }

        [ForeignKey(nameof(GrnHeaderId))]
        public virtual GrnHeader? GrnHeader { get; set; }

        // Supplier's invoice number copied from GRN at return time.
        // Stored as snapshot because supplier invoice text may be edited later.
        [MaxLength(50)]
        public string OriginalInvoiceNo { get; set; } = string.Empty;

        // =========================================================
        // HEADER DETAILS
        // =========================================================

        [Required]
        public DateTime ReturnDate { get; set; } = DateTime.Now;

        [Required]
        [MaxLength(50)]
        public string AuthorizedBy { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Remarks { get; set; } = string.Empty;

        // =========================================================
        // FINANCIAL VALUES
        // =========================================================

        // Sum of line credit values before deductions.
        [Column(TypeName = "decimal(18,2)")]
        public decimal GrossCredit { get; set; } = 0m;

        // Optional deduction from supplier credit.
        // Example: supplier accepts return but charges restocking/handling fee.
        [Column(TypeName = "decimal(18,2)")]
        public decimal RestockingFee { get; set; } = 0m;

        // GrossCredit - RestockingFee.
        // This is the amount used to reduce supplier balance.
        [Column(TypeName = "decimal(18,2)")]
        public decimal NetCredit { get; set; } = 0m;

        // =========================================================
        // TAX REVERSAL SNAPSHOT FOUNDATION
        // =========================================================

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TaxableAmountTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TotalVatAmount { get; set; }

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
        public string TaxSnapshotStatus { get; set; } = "LegacyUnknown";

        // =========================================================
        // STATUS / AUDIT
        // =========================================================

        // Final workflow for now:
        // Posted, Cancelled.
        // Draft is intentionally not used.
        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = "Posted";

        [MaxLength(50)]
        public string CreatedBy { get; set; } = string.Empty;

        [MaxLength(50)]
        public string PostedBy { get; set; } = string.Empty;

        [MaxLength(50)]
        public string CancelledBy { get; set; } = string.Empty;

        [MaxLength(250)]
        public string CancellationReason { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public DateTime? PostedAt { get; set; }

        public DateTime? CancelledAt { get; set; }

        public virtual ICollection<SupplierReturnLine> ReturnLines { get; set; } = new List<SupplierReturnLine>();

        // =========================================================
        // UI / HELPER PROPERTIES
        // =========================================================

        [NotMapped]
        public bool IsPosted =>
            Status.Equals("Posted", StringComparison.OrdinalIgnoreCase);

        [NotMapped]
        public bool IsCancelled =>
            Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase);

        [NotMapped]
        public bool CanCancel => IsPosted && !IsCancelled;

        [NotMapped]
        public bool IsEditable => !IsPosted && !IsCancelled;

        [NotMapped]
        public string DisplayInvoiceNo =>
            string.IsNullOrWhiteSpace(OriginalInvoiceNo)
                ? "-"
                : OriginalInvoiceNo.Trim();

        [NotMapped]
        public string DisplayStatus =>
            string.IsNullOrWhiteSpace(Status)
                ? "-"
                : Status.Trim();
    }
}