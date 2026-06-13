using System;
using System.ComponentModel.DataAnnotations;

namespace POS.Core.Models
{
    public class UnitOfMeasure
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(10)]
        public string UomCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string UomDescription { get; set; } = string.Empty;

        // Critical rule for Point of Sale and Stock Adjustments
        public bool AllowDecimals { get; set; } = false;

        // Matches the XAML CheckBox logic
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}