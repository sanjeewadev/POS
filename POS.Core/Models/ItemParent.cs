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

        [MaxLength(20)]
        public string TaxCode { get; set; } = string.Empty;

        // =========================================================
        // STOCK / POS TRACKING RULES
        // =========================================================

        // Correct new rule:
        // true = this item uses stock batches/layers.
        // Default should be true for your system because cashier sale will select batch.
        public bool HasBatchTracking { get; set; } = true;

        // Correct new rule:
        // true = GRN must require expiry date.
        // Expiry tracking is separate from batch tracking.
        public bool HasExpiryTracking { get; set; } = false;

        // Legacy field.
        // Old code used this as "Batch / Expiry".
        // Keep temporarily until GRN, stock, and reports are fully moved to
        // HasBatchTracking + HasExpiryTracking.
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