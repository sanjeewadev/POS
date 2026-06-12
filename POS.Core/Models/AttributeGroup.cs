using System;
using System.Collections.Generic;

namespace POS.Core.Models
{
    public class AttributeGroup
    {
        public int Id { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // One-to-Many: A group has many values (e.g., Color -> Red, Blue, Green)
        public ICollection<AttributeValue> AttributeValues { get; set; } = new List<AttributeValue>();

        // Many-to-Many: A group belongs to many categories (e.g., Size -> Shirts, Shoes)
        public ICollection<Category> Categories { get; set; } = new List<Category>();
    }
}