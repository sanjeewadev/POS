using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace POS.Core.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string CategoryCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string CategoryName { get; set; } = string.Empty;

        // Used instead of hard delete.
        // Deactivated categories should not appear when creating new items.
        public bool IsDeactivated { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public DateTime? DeactivatedAt { get; set; }

        // One Category can have many SubCategories.
        public ICollection<SubCategory> SubCategories { get; set; } = new List<SubCategory>();

        // Explicit many-to-many relationship:
        // Category -> CategoryAttributeGroup -> AttributeGroup
        public ICollection<CategoryAttributeGroup> CategoryAssignments { get; set; } = new List<CategoryAttributeGroup>();
    }
}