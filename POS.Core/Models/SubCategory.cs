using System;
using System.ComponentModel.DataAnnotations;

namespace POS.Core.Models
{
    public class SubCategory
    {
        public int Id { get; set; }

        // Foreign key to Category
        public int CategoryId { get; set; }

        // Navigation property
        public Category Category { get; set; } = null!;

        [Required]
        [MaxLength(20)]
        public string SubCategoryCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string SubCategoryName { get; set; } = string.Empty;

        // Used instead of hard delete.
        // Deactivated sub-categories should not appear when creating new items.
        public bool IsDeactivated { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public DateTime? DeactivatedAt { get; set; }
    }
}