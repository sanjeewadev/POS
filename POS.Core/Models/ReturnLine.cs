using System;
using System.ComponentModel.DataAnnotations;

namespace POS.Core.Models
{
    public class ReturnLine
    {
        public int Id { get; set; }

        // Foreign Key to the Header
        public int ReturnHeaderId { get; set; }
        public ReturnHeader ReturnHeader { get; set; } = null!;

        // Foreign Key to the exact Matrix Variant (e.g., "Linen Shirt - Red - XL")
        [Required]
        public int ItemVariantId { get; set; }
        public ItemVariant ItemVariant { get; set; } = null!;

        // --- RETURN METRICS ---
        public decimal ReturnQty { get; set; } = 0m;

        [MaxLength(100)]
        public string ReasonCode { get; set; } = string.Empty; // e.g., "Wrong Item Delivered", "Damaged"

        // --- FINANCIAL IMPACT ---

        // The exact Unit Cost from the ORIGINAL Goods Received Note (GRN)
        public decimal HistoricalCost { get; set; } = 0m;

        // The financial credit value (ReturnQty * HistoricalCost)
        public decimal CreditValue { get; set; } = 0m;
    }
}