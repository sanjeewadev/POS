using System;
using System.Collections.Generic;

namespace POS.Core.Models.DTOs
{
    public class StockBalanceDto
    {
        public int VariantId { get; set; }

        public string ItemCode { get; set; } = string.Empty;
        public string SkuCode { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;

        public string VariantDescription { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Uom { get; set; } = string.Empty;

        public string CategoryName { get; set; } = string.Empty;
        public string PrimarySupplierName { get; set; } = string.Empty;

        public decimal TotalQtyOnHand { get; set; }

        // Backward-compatible alias if old code still uses QtyOnHand.
        public decimal QtyOnHand => TotalQtyOnHand;

        public decimal UnitCost { get; set; }
        public decimal UnitRetail { get; set; }
        public decimal UnitWholesale { get; set; }

        public decimal TotalCostValue { get; set; }
        public decimal TotalRetailValue { get; set; }
        public decimal TotalWholesaleValue { get; set; }

        public decimal PotentialGrossProfit => TotalRetailValue - TotalCostValue;

        public decimal MarkupPercent =>
            TotalCostValue <= 0 ? 0 : Math.Round((PotentialGrossProfit / TotalCostValue) * 100m, 2);

        public int BatchCount { get; set; }

        public bool HasNegativeStock => TotalQtyOnHand < 0;
        public bool HasZeroStock => TotalQtyOnHand == 0;
        public bool HasPositiveStock => TotalQtyOnHand > 0;

        public bool HasExpiredBatch { get; set; }
        public bool HasExpiringSoonBatch { get; set; }

        public string StockStatus { get; set; } = "Normal";

        public DateTime? EarliestExpiryDate { get; set; }
        public DateTime? LastReceivedDate { get; set; }

        public List<ItemBatchDto> Batches { get; set; } = new();
    }

    public class ItemBatchDto
    {
        public int BatchId { get; set; }
        public int ItemVariantId { get; set; }

        public string BatchNo { get; set; } = string.Empty;

        public DateTime? ExpiryDate { get; set; }
        public DateTime ReceivedDate { get; set; }

        public decimal CurrentStock { get; set; }

        public decimal CostPrice { get; set; }
        public decimal RetailPrice { get; set; }
        public decimal WholesalePrice { get; set; }

        public decimal TotalBatchCost => Math.Round(CurrentStock * CostPrice, 2);
        public decimal TotalBatchRetail => Math.Round(CurrentStock * RetailPrice, 2);
        public decimal TotalBatchWholesale => Math.Round(CurrentStock * WholesalePrice, 2);

        public bool IsDeactivated { get; set; }

        public int? DaysToExpire =>
            ExpiryDate.HasValue
                ? (ExpiryDate.Value.Date - DateTime.Now.Date).Days
                : null;

        public string ExpiryStatus
        {
            get
            {
                if (!ExpiryDate.HasValue)
                    return "No Expiry";

                if (ExpiryDate.Value.Date < DateTime.Now.Date)
                    return "Expired";

                if (ExpiryDate.Value.Date <= DateTime.Now.Date.AddDays(30))
                    return "Expiring Soon";

                return "OK";
            }
        }

        public string StockStatus
        {
            get
            {
                if (CurrentStock < 0)
                    return "Negative";

                if (CurrentStock == 0)
                    return "Zero";

                return "In Stock";
            }
        }
    }
}