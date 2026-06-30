using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class FreeItemClaimLog
    {
        [Key]
        public int Id { get; set; }

        // =========================================================
        // SALES REFERENCES
        // =========================================================

        public int SalesHeaderId { get; set; }

        public int SalesLineId { get; set; }

        [Required]
        [MaxLength(50)]
        public string InvoiceNo { get; set; } = string.Empty;

        public DateTime InvoiceDate { get; set; } = DateTime.Now;

        [MaxLength(100)]
        public string CashierName { get; set; } = string.Empty;

        [MaxLength(20)]
        public string TerminalNo { get; set; } = string.Empty;

        // =========================================================
        // RULE / REASON SNAPSHOT
        // =========================================================

        public int? FreeIssueRuleId { get; set; }

        [MaxLength(150)]
        public string FreeIssueRuleName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string FreeReasonCode { get; set; } = string.Empty;

        [MaxLength(150)]
        public string FreeReasonText { get; set; } = string.Empty;

        // SupplierClaim is expected here, but keeping type gives clean audit.
        [Required]
        [MaxLength(30)]
        public string FreeIssueType { get; set; } = "SupplierClaim";

        // =========================================================
        // SUPPLIER
        // =========================================================

        public int? SupplierId { get; set; }

        [MaxLength(150)]
        public string SupplierName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string SupplierPromotionReference { get; set; } = string.Empty;

        // =========================================================
        // ITEM SNAPSHOT
        // =========================================================

        public int? ItemVariantId { get; set; }

        public int? ItemBatchId { get; set; }

        [MaxLength(100)]
        public string Barcode { get; set; } = string.Empty;

        [MaxLength(100)]
        public string SkuCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(250)]
        public string ItemDescription { get; set; } = string.Empty;

        [MaxLength(100)]
        public string BatchNo { get; set; } = string.Empty;

        public DateTime? ExpiryDate { get; set; }

        [MaxLength(30)]
        public string Uom { get; set; } = "PCS";

        // =========================================================
        // VALUES
        // =========================================================

        [Column(TypeName = "decimal(18,3)")]
        public decimal Quantity { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal CostPrice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal OriginalUnitPrice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal FreeIssueCostValue { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal FreeIssueSellingValue { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal ClaimValue { get; set; } = 0m;

        // =========================================================
        // CLAIM STATUS
        // =========================================================
        // Pending / Submitted / Settled / Rejected / Written Off / Cancelled

        [Required]
        [MaxLength(30)]
        public string ClaimStatus { get; set; } = "Pending";

        [MaxLength(100)]
        public string ClaimReferenceNo { get; set; } = string.Empty;

        public DateTime? SubmittedAt { get; set; }

        [MaxLength(100)]
        public string SubmittedBy { get; set; } = string.Empty;

        public DateTime? SettledAt { get; set; }

        [MaxLength(100)]
        public string SettledBy { get; set; } = string.Empty;

        // Credit Note / Replacement Stock / Cash / Other
        [MaxLength(50)]
        public string SettlementType { get; set; } = string.Empty;

        [MaxLength(100)]
        public string SettlementReferenceNo { get; set; } = string.Empty;

        public DateTime? RejectedAt { get; set; }

        [MaxLength(100)]
        public string RejectedBy { get; set; } = string.Empty;

        [MaxLength(300)]
        public string RejectReason { get; set; } = string.Empty;

        public DateTime? WrittenOffAt { get; set; }

        [MaxLength(100)]
        public string WrittenOffBy { get; set; } = string.Empty;

        [MaxLength(300)]
        public string WriteOffReason { get; set; } = string.Empty;

        public DateTime? CancelledAt { get; set; }

        [MaxLength(100)]
        public string CancelledBy { get; set; } = string.Empty;

        [MaxLength(300)]
        public string CancelReason { get; set; } = string.Empty;

        // =========================================================
        // APPROVAL / AUDIT
        // =========================================================

        [MaxLength(100)]
        public string FreeApprovedBy { get; set; } = string.Empty;

        public DateTime? FreeApprovedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime? UpdatedAt { get; set; }

        [MaxLength(100)]
        public string UpdatedBy { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Remarks { get; set; } = string.Empty;

        // =========================================================
        // NAVIGATION
        // =========================================================

        [ForeignKey(nameof(SalesHeaderId))]
        public virtual SalesHeader? SalesHeader { get; set; }

        [ForeignKey(nameof(SalesLineId))]
        public virtual SalesLine? SalesLine { get; set; }

        // =========================================================
        // DISPLAY HELPERS
        // =========================================================

        [NotMapped]
        public bool IsPending =>
            ClaimStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase);

        [NotMapped]
        public bool IsSubmitted =>
            ClaimStatus.Equals("Submitted", StringComparison.OrdinalIgnoreCase);

        [NotMapped]
        public bool IsSettled =>
            ClaimStatus.Equals("Settled", StringComparison.OrdinalIgnoreCase);

        [NotMapped]
        public bool IsRejected =>
            ClaimStatus.Equals("Rejected", StringComparison.OrdinalIgnoreCase);

        [NotMapped]
        public bool IsWrittenOff =>
            ClaimStatus.Equals("Written Off", StringComparison.OrdinalIgnoreCase);

        [NotMapped]
        public bool IsCancelled =>
            ClaimStatus.Equals("Cancelled", StringComparison.OrdinalIgnoreCase);

        [NotMapped]
        public string DisplaySupplier =>
            string.IsNullOrWhiteSpace(SupplierName) ? "Unknown Supplier" : SupplierName;

        [NotMapped]
        public string DisplayClaimStatus =>
            string.IsNullOrWhiteSpace(ClaimStatus) ? "Pending" : ClaimStatus;
    }
}