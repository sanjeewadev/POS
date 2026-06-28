using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class StockAdjustmentLine
    {
        public int Id { get; set; }

        public int StockAdjustmentHeaderId { get; set; }

        public StockAdjustmentHeader StockAdjustmentHeader { get; set; } = null!;

        [Required]
        public int ItemBatchId { get; set; }

        public ItemBatch ItemBatch { get; set; } = null!;

        // Saved snapshot for easier reporting.
        [Required]
        public int ItemVariantId { get; set; }

        public ItemVariant ItemVariant { get; set; } = null!;

        // =========================================================
        // UI HELPERS - NOT SAVED
        // =========================================================

        [NotMapped]
        public string ItemCode { get; set; } = string.Empty;

        [NotMapped]
        public string VariantDescription { get; set; } = string.Empty;

        [NotMapped]
        public string Description { get; set; } = string.Empty;

        [NotMapped]
        public string BatchNo { get; set; } = string.Empty;

        [NotMapped]
        public DateTime? ExpiryDate { get; set; }

        // =========================================================
        // QUANTITY SNAPSHOT
        // =========================================================

        [Column(TypeName = "decimal(18,3)")]
        public decimal SystemQty { get; set; } = 0m;

        [Column(TypeName = "decimal(18,3)")]
        public decimal ActualQty { get; set; } = 0m;

        // ActualQty - SystemQty.
        // Positive = stock increase.
        // Negative = stock decrease.
        [Column(TypeName = "decimal(18,3)")]
        public decimal VarianceQty { get; set; } = 0m;

        // =========================================================
        // REASON / ACCOUNTING
        // =========================================================

        // Damage / Broken, Expired / Spoiled, Theft / Missing,
        // Found Stock, Data Entry Error, Opening Balance, Audit Correction, etc.
        [MaxLength(50)]
        public string ReasonCode { get; set; } = string.Empty;

        [MaxLength(250)]
        public string LineRemarks { get; set; } = string.Empty;

        // =========================================================
        // FINANCIAL SNAPSHOT
        // =========================================================

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitCost { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal CostImpact { get; set; } = 0m;

        // Open, Posted, Cancelled.
        [MaxLength(30)]
        public string LineStatus { get; set; } = "Open";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}