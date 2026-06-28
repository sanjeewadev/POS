using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace POS.Core.Models
{
    public class AttributeGroup
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string GroupName { get; set; } = string.Empty;

        // Controls display order in Item Property and Item Master pages.
        // Example: Color first, Size second, Storage third.
        public int DisplayOrder { get; set; } = 0;

        // Used instead of hard delete when the group is already used by item variants.
        public bool IsDeactivated { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public DateTime? DeactivatedAt { get; set; }

        // One Group has many Values.
        // Example: Color -> Red, Blue, Black
        public ICollection<AttributeValue> AttributeValues { get; set; } = new List<AttributeValue>();

        // Many-to-many link to Categories.
        // Example: Color is assigned to Fashion, Shoes, Electronics.
        public ICollection<CategoryAttributeGroup> CategoryAssignments { get; set; } = new List<CategoryAttributeGroup>();
    }
}