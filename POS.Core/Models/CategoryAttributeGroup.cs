using System;

namespace POS.Core.Models
{
    public class CategoryAttributeGroup
    {
        // Composite primary key:
        // CategoryId + AttributeGroupId
        // This is configured in AppDbContext.

        public int CategoryId { get; set; }
        public Category Category { get; set; } = null!;

        public int AttributeGroupId { get; set; }
        public AttributeGroup AttributeGroup { get; set; } = null!;

        public DateTime AssignedAt { get; set; } = DateTime.Now;
    }
}