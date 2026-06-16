using System;
using System.ComponentModel.DataAnnotations;

namespace POS.Core.Models
{
    public class ItemBatch
    {
        public int Id { get; set; }

        // Link to the specific Matrix Variant (e.g., "Red Shirt - Large")
        [Required]
        public int ItemVariantId { get; set; }
        public ItemVariant ItemVariant { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string BatchNo { get; set; } = string.Empty;

        public DateTime? ExpiryDate { get; set; }
        public DateTime ReceivedDate { get; set; } = DateTime.Now;

        // --- BATCH-SPECIFIC PRICING ---
        // This batch keeps its own prices forever, ignoring global price changes!
        public decimal CostPrice { get; set; } = 0m;
        public decimal RetailPrice { get; set; } = 0m;
        public decimal WholesalePrice { get; set; } = 0m;

        // --- BATCH-SPECIFIC STOCK ---
        public decimal CurrentStock { get; set; } = 0m;

        public bool IsDeactivated { get; set; } = false;
    }
}