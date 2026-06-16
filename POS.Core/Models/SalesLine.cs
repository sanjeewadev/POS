using System;
using System.ComponentModel.DataAnnotations;

namespace POS.Core.Models
{
    public class SalesLine
    {
        public int Id { get; set; }

        public int SalesHeaderId { get; set; }
        public SalesHeader SalesHeader { get; set; } = null!;

        // CRITICAL: Linking directly to the Batch guarantees perfect stock and cost tracking
        [Required]
        public int ItemBatchId { get; set; }
        public ItemBatch ItemBatch { get; set; } = null!;

        [MaxLength(100)]
        public string ItemDescription { get; set; } = string.Empty;

        public decimal Quantity { get; set; } = 1m;

        // --- PRICING SNAPSHOT ---
        // We capture these at the exact moment of sale so historical records never change
        public decimal UnitPrice { get; set; } = 0m;
        public decimal CostPrice { get; set; } = 0m;

        // --- DISCOUNT & TOTALS ---
        public decimal DiscountPercentage { get; set; } = 0m;
        public decimal DiscountAmount { get; set; } = 0m;
        public decimal LineTotal { get; set; } = 0m;

        public bool IsReturned { get; set; } = false;
    }
}