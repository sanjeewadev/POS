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

        // Foreign Keys for Classification
        public int CategoryId { get; set; }
        public Category Category { get; set; } = null!;

        public int? SubCategoryId { get; set; }
        public SubCategory? SubCategory { get; set; }

        [MaxLength(20)]
        public string BaseUom { get; set; } = string.Empty;

        [MaxLength(20)]
        public string TaxCode { get; set; } = string.Empty;

        // Physical Logistics Options
        public bool IsScaleItem { get; set; }
        public bool HasBatchExpiry { get; set; }
        public bool IsSerialized { get; set; }

        // POS Matrix Restrictions
        public bool AllowCashierDiscount { get; set; }

        // THE FIX: Put this back so the database doesn't crash. 
        // It stays hidden from the UI.
        public bool IsPurchaseLocked { get; set; }

        public bool IsSaleLocked { get; set; }
        public bool IsDeactivated { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation: One Parent dictates Many Variants
        public ICollection<ItemVariant> Variants { get; set; } = new List<ItemVariant>();
    }
}