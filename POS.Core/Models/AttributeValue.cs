using System;
using System.ComponentModel.DataAnnotations;

namespace POS.Core.Models
{
    public class AttributeValue
    {
        public int Id { get; set; }

        // Foreign Key linking back to AttributeGroup
        public int AttributeGroupId { get; set; }

        // Navigation Property for Entity Framework JOINs
        public AttributeGroup AttributeGroup { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string ValueName { get; set; } = string.Empty;

        public bool IsDeactivated { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}