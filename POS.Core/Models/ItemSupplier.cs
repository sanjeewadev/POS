using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class ItemSupplier
    {
        [Key]
        public int Id { get; set; }

        // ==========================================
        // 1. THE FOREIGN KEYS (The Bridge)
        // ==========================================

        public int ItemVariantId { get; set; }
        [ForeignKey("ItemVariantId")]
        public virtual ItemVariant ItemVariant { get; set; }

        public int SupplierId { get; set; }
        [ForeignKey("SupplierId")]
        public virtual Supplier Supplier { get; set; }

        // ==========================================
        // 2. SUPPLIER-SPECIFIC DATA
        // ==========================================

        [MaxLength(100)]
        public string? SupplierItemCode { get; set; } // What the supplier calls this item in their catalog

        [Column(TypeName = "decimal(18,2)")]
        public decimal LastCostPrice { get; set; } // The specific price THIS supplier charges you

        public bool IsPrimary { get; set; } // True if this is the default supplier for auto-generating POs

        public int MinimumOrderQuantity { get; set; } = 1; // Supplier's MOQ (e.g., "Must order in boxes of 12")

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}