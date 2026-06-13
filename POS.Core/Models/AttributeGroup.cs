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

        public bool IsDeactivated { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation: One Group has Many Values (e.g., "Color" -> "Red", "Blue")
        public ICollection<AttributeValue> AttributeValues { get; set; } = new List<AttributeValue>();

        // Navigation: Many-to-Many link to Categories (e.g., "Color" is assigned to "Shirts")
        public ICollection<CategoryAttributeGroup> CategoryAssignments { get; set; } = new List<CategoryAttributeGroup>();
    }
}