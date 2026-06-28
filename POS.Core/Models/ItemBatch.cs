using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class ItemBatch
    {
        public int Id { get; set; }

        [Required]
        public int ItemVariantId { get; set; }

        public ItemVariant ItemVariant { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string BatchNo { get; set; } = string.Empty;

        public DateTime? ExpiryDate { get; set; }

        public DateTime ReceivedDate { get; set; } = DateTime.Now;

        // =========================================================
        // BATCH COST / PRICE SNAPSHOT
        // =========================================================

        [Column(TypeName = "decimal(18,2)")]
        public decimal CostPrice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal RetailPrice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal WholesalePrice { get; set; } = 0m;

        // =========================================================
        // STOCK
        // =========================================================

        [Column(TypeName = "decimal(18,3)")]
        public decimal CurrentStock { get; set; } = 0m;

        public bool IsDeactivated { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public DateTime? DeactivatedAt { get; set; }

        [NotMapped]
        public bool HasExpiry => ExpiryDate.HasValue;

        [NotMapped]
        public decimal StockValue => CurrentStock * CostPrice;
    }
}