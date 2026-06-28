using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class ItemSupplier
    {
        public int Id { get; set; }

        // =========================================================
        // BRIDGE KEYS
        // =========================================================

        public int ItemVariantId { get; set; }

        public ItemVariant ItemVariant { get; set; } = null!;

        public int SupplierId { get; set; }

        public Supplier Supplier { get; set; } = null!;

        // =========================================================
        // SUPPLIER-SPECIFIC ITEM DATA
        // =========================================================

        // What the supplier calls this item in their invoice/catalog.
        // Example:
        // Your SKU: TSHIRT-RED-M
        // Supplier SKU: ABC-7788
        [MaxLength(100)]
        public string SupplierItemCode { get; set; } = string.Empty;

        // Last known cost from this supplier.
        // GRN should update this later when real purchase cost changes.
        [Column(TypeName = "decimal(18,2)")]
        public decimal LastCostPrice { get; set; } = 0m;

        // True if this supplier is the preferred/default supplier for this variant.
        // AppDbContext will enforce only one primary supplier per variant.
        public bool IsPrimary { get; set; } = false;

        // Supplier's minimum order quantity.
        // Example: must order at least 12 pieces.
        public int MinimumOrderQuantity { get; set; } = 1;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}