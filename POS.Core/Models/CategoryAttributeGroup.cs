using System;

namespace POS.Core.Models
{
    public class CategoryAttributeGroup
    {
        // Note: The composite primary key (CategoryId + AttributeGroupId) 
        // will be automatically configured in your AppDbContext.

        // Link to Category
        public int CategoryId { get; set; }
        public Category Category { get; set; } = null!;

        // Link to Attribute Group
        public int AttributeGroupId { get; set; }
        public AttributeGroup AttributeGroup { get; set; } = null!;

        // Audit trail for when this assignment was created
        public DateTime AssignedAt { get; set; } = DateTime.Now;
    }
}