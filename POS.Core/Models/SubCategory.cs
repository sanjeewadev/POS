using System;

namespace POS.Core.Models
{
    public class SubCategory
    {
        public int Id { get; set; }

        // Foreign Key
        public int CategoryId { get; set; }

        // Navigation Property (Crucial for UI Joins)
        public Category Category { get; set; } = null!;

        public string SubCategoryCode { get; set; } = string.Empty;
        public string SubCategoryName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}