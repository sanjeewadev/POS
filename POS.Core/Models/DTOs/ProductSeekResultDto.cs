using System;

namespace POS.Core.Models.DTOs
{
    /// <summary>
    /// Represents a flattened, simplified product result for the cashier's product seek/search window.
    /// This combines information from ItemParent and ItemVariant for a user-friendly display.
    /// </summary>
    public class ProductSeekResultDto
    {
        public int VariantId { get; set; }

        public int ParentId { get; set; }

        public string ItemCode { get; set; } = string.Empty;

        public string ItemName { get; set; } = string.Empty;

        public string SkuCode { get; set; } = string.Empty;

        public string Barcode { get; set; } = string.Empty;

        public string VariantDescription { get; set; } = string.Empty;

        public string CategoryName { get; set; } = string.Empty;

        public decimal RetailPrice { get; set; }

        public decimal WholesalePrice { get; set; }

        public decimal StockOnHand { get; set; }

        public bool HasBatchTracking { get; set; }

        public bool IsService { get; set; }
    }
}