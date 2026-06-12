using System;

namespace POS.Core.Models
{
    public class AttributeValue
    {
        public int Id { get; set; }

        // Foreign Key
        public int AttributeGroupId { get; set; }
        public AttributeGroup AttributeGroup { get; set; } = null!;

        public string ValueName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}