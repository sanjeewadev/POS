using System;
using System.ComponentModel.DataAnnotations;

namespace POS.Core.Models
{
    public class AttributeValue
    {
        public int Id { get; set; }

        // Foreign key linking back to AttributeGroup.
        public int AttributeGroupId { get; set; }

        // Navigation property for EF joins.
        public AttributeGroup AttributeGroup { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string ValueName { get; set; } = string.Empty;

        // Controls display order inside the selected group.
        // Example:
        // Size -> XS, S, M, L, XL
        // Waist -> 30, 32, 34, 36
        public int DisplayOrder { get; set; } = 0;

        // Used instead of hard delete when this value is already used by item variants.
        public bool IsDeactivated { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public DateTime? DeactivatedAt { get; set; }
    }
}