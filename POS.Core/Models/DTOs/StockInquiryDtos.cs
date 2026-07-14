using System;

namespace POS.Core.Models.DTOs
{
    public sealed class StockInquiryResultDto
    {
        public int ItemVariantId { get; set; }

        public string ItemCode { get; set; } = string.Empty;

        public string SkuCode { get; set; } = string.Empty;

        public string Barcode { get; set; } = string.Empty;

        public string ItemName { get; set; } = string.Empty;

        public string VariantDescription { get; set; } = string.Empty;

        public decimal CurrentStock { get; set; }

        public string DisplayName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(VariantDescription) ||
                    VariantDescription.Equals("Standard", StringComparison.OrdinalIgnoreCase))
                {
                    return ItemName;
                }

                return $"{ItemName} - {VariantDescription}";
            }
        }

        public string LookupCode
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(SkuCode))
                    return SkuCode;

                if (!string.IsNullOrWhiteSpace(ItemCode))
                    return ItemCode;

                return Barcode;
            }
        }
    }
}
