using System;

namespace POS.Core.Models.DTOs
{
    public class ProductSeekResultDto
    {
        public int VariantId { get; set; }
        public int ParentId { get; set; }
        public int? BatchId { get; set; } // For exact batch barcode scan or single-batch selection
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string SkuCode { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string VariantDescription { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public decimal RetailPrice { get; set; }
        public decimal WholesalePrice { get; set; }
        public bool IsService { get; set; }
        public bool HasBatchTracking { get; set; }
        public decimal StockOnHand { get; set; }
        public DateTime? EarliestExpiryDate { get; set; }
        public string EarliestExpiryDateDisplayText => EarliestExpiryDate?.ToString("yyyy-MM-dd") ?? "-";
    }
}