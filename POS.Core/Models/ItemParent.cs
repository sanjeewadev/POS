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

        public int UnitOfMeasureId { get; set; } = 1;

        public UnitOfMeasure UnitOfMeasure { get; set; } = null!;

        // Legacy helper field.
        // Keep this while older pages still read BaseUom directly.
        [MaxLength(20)]
        public string BaseUom { get; set; } = string.Empty;

        // =========================================================
        // TAX
        // =========================================================

        // Temporary until Tax Master is added.
        // Current accepted values:
        // TAX-FREE = 0%
        // VAT-STD  = standard VAT rate from Tax Master later
        // VAT-RED  = reduced VAT rate from Tax Master later
        [MaxLength(20)]
        public string TaxCode { get; set; } = "TAX-FREE";

        // Default purchase cost entry mode.
        // false = supplier cost is normally VAT exclusive.
        // true  = supplier cost is normally VAT inclusive.
        // PO/GRN can still store the final transaction flag per line.
        public bool IsTaxInclusive { get; set; } = false;

        // =========================================================
        // STOCK / POS TRACKING RULES
        // =========================================================

        // true = this item uses stock batches/layers.
        public bool HasBatchTracking { get; set; } = true;

        // true = GRN must require expiry date.
        public bool HasExpiryTracking { get; set; } = false;

        // Legacy field kept for compatibility while old code is migrated.
        public bool HasBatchExpiry { get; set; } = false;

        public bool IsScaleItem { get; set; } = false;

        // Keep for future serial workflow, but do not show in Item Master UI yet.
        public bool IsSerialized { get; set; } = false;

        public bool AllowCashierDiscount { get; set; } = true;

        public bool IsPurchaseLocked { get; set; } = false;

        public bool IsSaleLocked { get; set; } = false;

        public bool IsDeactivated { get; set; } = false;

        // =========================================================
        // AUDIT
        // =========================================================

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public DateTime? DeactivatedAt { get; set; }

        // =========================================================
        // NAVIGATION
        // =========================================================

        public ICollection<ItemVariant> Variants { get; set; } = new List<ItemVariant>();
    }
}
