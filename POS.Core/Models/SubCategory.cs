using System;
using System.ComponentModel.DataAnnotations;

namespace POS.Core.Models
{
    public class SubCategory
    {
        public int Id { get; set; }

        // =========================================================
        // PARENT CATEGORY
        // =========================================================

        public int CategoryId { get; set; }

        public Category Category { get; set; } = null!;

        // Final code can be generated as:
        // CategoryCode + "-" + user suffix
        // Example: 001-01, 001-SOFT, GROC-BISCUIT
        [Required]
        [MaxLength(40)]
        public string SubCategoryCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string SubCategoryName { get; set; } = string.Empty;

        public int DisplayOrder { get; set; } = 0;

        // Used instead of hard delete.
        // Deactivated sub-categories should not appear when creating new items.
        public bool IsDeactivated { get; set; } = false;

        // =========================================================
        // AUDIT
        // =========================================================

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [MaxLength(100)]
        public string UpdatedBy { get; set; } = string.Empty;

        public DateTime? DeactivatedAt { get; set; }

        [MaxLength(100)]
        public string DeactivatedBy { get; set; } = string.Empty;
    }
}