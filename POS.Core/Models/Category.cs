using System;

namespace POS.Core.Models
{
    public class Category
    {
        public int Id { get; set; }
        public string CategoryCode { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Add this inside your existing Category class:
        public ICollection<AttributeGroup> AttributeGroups { get; set; } = new List<AttributeGroup>();
    }
}