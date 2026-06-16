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

        // --- UI HELPERS (Not saved to DB, just for the WPF DataGrid) ---
        [NotMapped] public string ItemCode { get; set; } = string.Empty;
        [NotMapped] public string VariantDescription { get; set; } = string.Empty;
        [NotMapped] public string Description { get; set; } = string.Empty;
        [NotMapped] public string BatchNo { get; set; } = string.Empty;
        [NotMapped] public DateTime? ExpiryDate { get; set; }

        // --- THE SNAPSHOT METRICS ---
        public decimal SystemQty { get; set; } = 0m;
        public decimal ActualQty { get; set; } = 0m;
        public decimal VarianceQty { get; set; } = 0m;

        [MaxLength(50)]
        public string ReasonCode { get; set; } = string.Empty;

        // --- FINANCIAL IMPACT ---
        public decimal UnitCost { get; set; } = 0m;
        public decimal CostImpact { get; set; } = 0m;
    }
}