using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class LoyaltyDiscountProfile
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string ProfileName { get; set; } = string.Empty; // e.g., "VIP 10% Off", "Employee Flat Rs.500"

        [Required]
        [MaxLength(50)]
        public string DiscountType { get; set; } = "Percentage"; // "Percentage" or "FixedAmount"

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountValue { get; set; } // The actual math value (10 for 10%, 500 for Rs.500)

        [Required]
        [MaxLength(50)]
        public string Scope { get; set; } = "All"; // "All Products", "Specific Category", "Specific Item"

        [MaxLength(100)]
        public string TargetCode { get; set; } = string.Empty; // e.g., "BEVERAGES" or "ITEM-102" (Leave blank if Scope is "All")

        public bool IsActive { get; set; } = true;
    }
}