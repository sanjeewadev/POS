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

        // Perfectly aligned with your new XAML UI logic
        public bool IsDeactivated { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation property for Matrix/Variants
        public ICollection<AttributeGroup> AttributeGroups { get; set; } = new List<AttributeGroup>();
    }
}