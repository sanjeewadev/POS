using System;
using System.ComponentModel.DataAnnotations;

namespace POS.Core.Models
{
    public class SubCategory
    {
        public int Id { get; set; }

        // Foreign Key
        public int CategoryId { get; set; }

        // Navigation Property (Crucial for UI Joins and Entity Framework)
        public Category Category { get; set; } = null!;

        [Required]
        [MaxLength(20)]
        public string SubCategoryCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string SubCategoryName { get; set; } = string.Empty;

        // Perfectly aligned with your XAML UI logic
        public bool IsDeactivated { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}