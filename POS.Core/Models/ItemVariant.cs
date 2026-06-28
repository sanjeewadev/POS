using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class ItemVariant
    {
        public int Id { get; set; }

        // =========================================================
        // PARENT LINK
        // =========================================================

        public int ItemParentId { get; set; }

        public ItemParent ItemParent { get; set; } = null!;

        // =========================================================
        // VARIANT IDENTITY
        // =========================================================

        [Required]
        [MaxLength(100)]
        public string SkuCode { get; set; } = string.Empty;

        // Example:
        // Red / Medium
        // 256GB / Black
        // Standard
        [MaxLength(250)]
        public string VariantDescription { get; set; } = string.Empty;

        // Internal barcode or supplier/manufacturer barcode.
        // Must be unique when not empty.
        [MaxLength(100)]
        public string Barcode { get; set; } = string.Empty;

        // =========================================================
        // PRICING
        // =========================================================

        [Column(TypeName = "decimal(18,2)")]
        public decimal AverageCost { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal CostPrice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal RetailPrice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal WholesalePrice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal MinimumPrice { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal MaximumPrice { get; set; } = 0m;

        public int ReorderLevel { get; set; } = 0;

        // Used instead of hard delete after the variant has history.
        public bool IsDeactivated { get; set; } = false;

        // Display-only for UI. Actual stock should be calculated from batches/transactions.
        [NotMapped]
        public decimal TotalStockOnHand { get; set; } = 0m;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public DateTime? DeactivatedAt { get; set; }

        // =========================================================
        // NAVIGATION
        // =========================================================

        // Example:
        // Color -> Red
        // Size  -> Medium
        public ICollection<ItemPropertyMapping> PropertyMappings { get; set; } = new List<ItemPropertyMapping>();

        // One variant can have many physical stock batches.
        public ICollection<ItemBatch> ItemBatches { get; set; } = new List<ItemBatch>();

        // Approved suppliers for this specific variant.
        public ICollection<ItemSupplier> ItemSuppliers { get; set; } = new List<ItemSupplier>();
    }
}