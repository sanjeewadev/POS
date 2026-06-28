using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace POS.Core.Models
{
    public class ItemParent
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string ItemCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string ItemName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string PrintName { get; set; } = string.Empty;

        // =========================================================
        // CLASSIFICATION
        // =========================================================

        public int CategoryId { get; set; }
        public Category Category { get; set; } = null!;

        public int? SubCategoryId { get; set; }
        public SubCategory? SubCategory { get; set; }

        // =========================================================
        // UNIT OF MEASURE
        // =========================================================
        // New correct relationship.
        // Item Master should save this value from the UOM dropdown.
        public int UnitOfMeasureId { get; set; } = 1;

        public UnitOfMeasure UnitOfMeasure { get; set; } = null!;

        // Temporary legacy field.
        // Keep this only until Item Master ViewModel/XAML/Repository are fully moved to UnitOfMeasureId.
        // New code should not use this.
        [MaxLength(20)]
        public string BaseUom { get; set; } = string.Empty;

        // =========================================================
        // TAX / LOGISTICS / POS RULES
        // =========================================================

        [MaxLength(20)]
        public string TaxCode { get; set; } = string.Empty;

        public bool IsScaleItem { get; set; }

        public bool HasBatchExpiry { get; set; }

        public bool IsSerialized { get; set; }

        public bool AllowCashierDiscount { get; set; }

        public bool IsPurchaseLocked { get; set; }

        public bool IsSaleLocked { get; set; }

        public bool IsDeactivated { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public DateTime? DeactivatedAt { get; set; }

        // One Parent dictates many Variants.
        // Example:
        // Parent: T-Shirt
        // Variants: T-Shirt / Red / M, T-Shirt / Blue / L
        public ICollection<ItemVariant> Variants { get; set; } = new List<ItemVariant>();
    }
}