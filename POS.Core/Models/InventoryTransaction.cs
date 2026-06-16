using System;
using System.ComponentModel.DataAnnotations;

namespace POS.Core.Models
{
    public class InventoryTransaction
    {
        public int Id { get; set; }

        // Links to the exact Matrix SKU (e.g., T-Shirt - Red - XL)
        [Required]
        public int ItemVariantId { get; set; }
        public ItemVariant ItemVariant { get; set; } = null!;

        [Required]
        public DateTime TransactionDate { get; set; } = DateTime.Now;

        // Classification: e.g., "GRN", "SALE", "RETURN", "ADJUSTMENT"
        [Required]
        [MaxLength(20)]
        public string TransactionType { get; set; } = string.Empty;

        // The exact document that caused this movement: e.g., "GRN-20260613-0001"
        [Required]
        [MaxLength(50)]
        public string ReferenceDocument { get; set; } = string.Empty;

        // CRITICAL: Positive numbers for IN (GRN/Returns), Negative numbers for OUT (Sales/Shrinkage)
        [Required]
        public decimal Quantity { get; set; }

        // Snapshot of the Landed Cost at the exact millisecond this happened. 
        // This makes historical profit reporting 100% accurate.
        public decimal UnitCost { get; set; }

        [MaxLength(50)]
        public string CreatedBy { get; set; } = string.Empty;

        [MaxLength(250)]
        public string Remarks { get; set; } = string.Empty;
    }
}