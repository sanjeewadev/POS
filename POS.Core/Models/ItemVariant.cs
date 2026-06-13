using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class ItemVariant
    {
        public int Id { get; set; }

        // Foreign Key linking back to the Parent Blueprint
        public int ItemParentId { get; set; }
        public ItemParent ItemParent { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string SkuCode { get; set; } = string.Empty;

        [MaxLength(250)]
        public string VariantDescription { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Barcode { get; set; } = string.Empty;

        // Financials (Isolated per variant)
        public decimal AverageCost { get; set; } = 0m;
        public decimal CostPrice { get; set; } = 0m;
        public decimal RetailPrice { get; set; } = 0m;
        public decimal WholesalePrice { get; set; } = 0m;
        public decimal MinimumPrice { get; set; } = 0m;

        public int ReorderLevel { get; set; } = 0;

        public bool IsDeactivated { get; set; } = false;

        // Transient property for the UI DataGrid. 
        // Actual stock is calculated dynamically from GRNs and Sales.
        [NotMapped]
        public decimal TotalStockOnHand { get; set; } = 0m;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation: The specific attributes (Color: Red, Size: XL) this variant possesses
        public ICollection<ItemPropertyMapping> PropertyMappings { get; set; } = new List<ItemPropertyMapping>();
    }
}