using System;

namespace POS.Core.Models
{
    public class StockBatch
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string BatchNumber { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; } // Nullable because hardware items don't expire
        public decimal CostPrice { get; set; }
        public decimal AvailableQty { get; set; }
    }
}