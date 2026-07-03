using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class SupplierReturnLine
    {
        [Key]
        public int Id { get; set; }

        // =========================================================
        // HEADER LINK
        // =========================================================

        [Required]
        public int ReturnHeaderId { get; set; }

        [ForeignKey(nameof(ReturnHeaderId))]
        public virtual SupplierReturnHeader? ReturnHeader { get; set; }

        // =========================================================
        // SOURCE GRN LINE
        // =========================================================

        // Business rule:
        // Supplier Return should be GRN-line based for now.
        // Kept nullable for compatibility, but repository must validate it.
        public int? GrnLineId { get; set; }

        [ForeignKey(nameof(GrnLineId))]
        public virtual GrnLine? GrnLine { get; set; }

        // =========================================================
        // ITEM / BATCH LINKS
        // =========================================================

        [Required]
        public int ItemVariantId { get; set; }

        [ForeignKey(nameof(ItemVariantId))]
        public virtual ItemVariant? ItemVariant { get; set; }

        // Critical:
        // Supplier return must deduct exact physical batch stock.
        [Required]
        public int ItemBatchId { get; set; }

        [ForeignKey(nameof(ItemBatchId))]
        public virtual ItemBatch? ItemBatch { get; set; }

        // Snapshot from ItemBatch at posting time.
        [MaxLength(50)]
        public string BatchNo { get; set; } = string.Empty;

        public DateTime? ExpiryDate { get; set; }

        // =========================================================
        // QUANTITY / VALUE
        // =========================================================

        [Column(TypeName = "decimal(18,3)")]
        public decimal ReturnQty { get; set; } = 0m;

        // Historical cost from GRN line.
        // Usually LandedCost, fallback UnitCost.
        [Column(TypeName = "decimal(18,2)")]
        public decimal HistoricalCost { get; set; } = 0m;

        // ReturnQty * HistoricalCost.
        [Column(TypeName = "decimal(18,2)")]
        public decimal CreditValue { get; set; } = 0m;

        // =========================================================
        // REASON / AUDIT
        // =========================================================

        [Required]
        [MaxLength(50)]
        public string ReasonCode { get; set; } = string.Empty;

        [MaxLength(250)]
        public string LineRemarks { get; set; } = string.Empty;

        // Posted, Cancelled.
        // Open/Draft is not used in final post-only workflow.
        [Required]
        [MaxLength(30)]
        public string LineStatus { get; set; } = "Posted";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // =========================================================
        // UI HELPERS - NOT SAVED
        // =========================================================

        [NotMapped]
        public string ItemCode { get; set; } = string.Empty;

        [NotMapped]
        public string Description { get; set; } = string.Empty;

        [NotMapped]
        public string VariantDescription { get; set; } = string.Empty;

        [NotMapped]
        public decimal CurrentBatchStock { get; set; } = 0m;

        [NotMapped]
        public decimal MaxReturnQty { get; set; } = 0m;

        [NotMapped]
        public bool IsPosted =>
            LineStatus.Equals("Posted", StringComparison.OrdinalIgnoreCase);

        [NotMapped]
        public bool IsCancelled =>
            LineStatus.Equals("Cancelled", StringComparison.OrdinalIgnoreCase);

        [NotMapped]
        public string DisplayDescription
        {
            get
            {
                if (string.IsNullOrWhiteSpace(VariantDescription) ||
                    VariantDescription.Equals("Standard", StringComparison.OrdinalIgnoreCase))
                {
                    return Description;
                }

                if (string.IsNullOrWhiteSpace(Description))
                    return VariantDescription;

                return $"{Description} - {VariantDescription}";
            }
        }

        [NotMapped]
        public string BatchDisplayText =>
            string.IsNullOrWhiteSpace(BatchNo)
                ? "-"
                : BatchNo.Trim();

        [NotMapped]
        public string ExpiryDisplayText =>
            ExpiryDate.HasValue
                ? ExpiryDate.Value.ToString("yyyy-MM-dd")
                : "No Expiry";

        [NotMapped]
        public decimal CalculatedCreditValue =>
            Math.Round(ReturnQty * HistoricalCost, 2);
    }
}