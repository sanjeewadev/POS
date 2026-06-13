using System;
using System.ComponentModel.DataAnnotations;

namespace POS.Core.Models
{
    public class Item
    {
        [Key]
        public int Id { get; set; }

        // The exact string the Ghost Scanner will catch
        [Required]
        [MaxLength(50)]
        public string Barcode { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Category { get; set; } = "General";

        // Financials
        public decimal CostPrice { get; set; } = 0.00m;
        public decimal RetailPrice { get; set; } = 0.00m;

        // Inventory Tracking
        public decimal StockQuantity { get; set; } = 0;

        // Soft Delete (So we never break old receipts by actually deleting an item)
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}