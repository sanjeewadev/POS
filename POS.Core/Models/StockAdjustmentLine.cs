using System;
using System.ComponentModel.DataAnnotations;

namespace POS.Core.Models
{
    public class StockAdjustmentLine
    {
        public int Id { get; set; }

        // Foreign Key to the Header
        public int StockAdjustmentHeaderId { get; set; }
        public StockAdjustmentHeader StockAdjustmentHeader { get; set; } = null!;

        // Foreign Key to the exact Matrix Variant (e.g., "Linen Shirt - Red - XL")
        [Required]
        public int ItemVariantId { get; set; }
        public ItemVariant ItemVariant { get; set; } = null!;

        // --- THE SNAPSHOT METRICS (Crucial for Auditing) ---

        // What the computer thought was on the shelf
        public decimal SystemQty { get; set; } = 0m;

        // What the warehouse clerk physically counted
        public decimal ActualQty { get; set; } = 0m;

        // The mathematical difference (ActualQty - SystemQty)
        public decimal VarianceQty { get; set; } = 0m;

        [MaxLength(50)]
        public string ReasonCode { get; set; } = string.Empty; // e.g., "Damaged / Broken", "Stolen"

        // --- FINANCIAL IMPACT ---

        // The exact Average Cost of the item at the specific second this adjustment was posted
        public decimal UnitCost { get; set; } = 0m;

        // The financial impact to the P&L (VarianceQty * UnitCost). 
        // Example: -2 Variance * Rs. 100 Cost = -Rs. 200 (Loss)
        public decimal CostImpact { get; set; } = 0m;
    }
}