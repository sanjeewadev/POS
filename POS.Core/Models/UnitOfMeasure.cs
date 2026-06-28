using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace POS.Core.Models
{
    public class UnitOfMeasure
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(10)]
        public string UomCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string UomDescription { get; set; } = string.Empty;

        // Critical rule for POS, GRN, stock adjustment, and sales quantity validation.
        // Example:
        // PCS -> false
        // KG  -> true
        // LTR -> true
        public bool AllowDecimals { get; set; } = false;

        // Controls display order in dropdowns.
        // Example:
        // PCS = 10
        // KG  = 20
        // LTR = 30
        public int DisplayOrder { get; set; } = 0;

        // Keep this because your current UI uses "Unit is Active".
        // Inactive UOMs should not appear when creating new items.
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public DateTime? DeactivatedAt { get; set; }

        // One UOM can be used by many item parents.
        // Example:
        // PCS -> Shirt, Phone, Book
        // KG  -> Rice, Sugar
        public ICollection<ItemParent> ItemParents { get; set; } = new List<ItemParent>();
    }
}