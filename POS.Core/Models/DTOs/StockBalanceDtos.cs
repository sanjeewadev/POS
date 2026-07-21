using System;
using System.Collections.Generic;
using System.Linq;

namespace POS.Core.Models.DTOs
{
    public class StockBalanceDto
    {
        // =========================================================
        // IDENTITY
        // =========================================================

        public int ParentId { get; set; }
        public int VariantId { get; set; }

        public string ItemCode { get; set; } = string.Empty;
        public string SkuCode { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
        public string VariantDescription { get; set; } = string.Empty;
        public string Uom { get; set; } = string.Empty;

        public string CategoryName { get; set; } = string.Empty;
        public string PrimarySupplierName { get; set; } = string.Empty;

        // =========================================================
        // TRACKING / COST METHOD
        // =========================================================

        public bool HasBatchTracking { get; set; }
        public bool HasExpiryTracking { get; set; }

        public bool IsAverageCostItem => !HasBatchTracking;
        public bool IsBatchTrackedItem => HasBatchTracking;
        public bool IsExpiryTrackedItem => HasBatchTracking && HasExpiryTracking;

        public string TrackingType
        {
            get
            {
                if (!HasBatchTracking)
                    return "AVERAGE";

                return HasExpiryTracking ? "BATCH_EXPIRY" : "BATCH";
            }
        }

        public string TrackingText
        {
            get
            {
                if (!HasBatchTracking)
                    return "Average Cost";

                return HasExpiryTracking ? "Batch + Expiry" : "Batch";
            }
        }

        public string CostMethodText
        {
            get
            {
                if (!HasBatchTracking)
                    return "Average Cost";

                return "Batch Weighted Cost";
            }
        }

        public string DetailsHeaderText
        {
            get
            {
                if (!HasBatchTracking)
                    return "AVERAGE-COST STOCK BUCKET";

                return HasExpiryTracking
                    ? "GRN BATCH + EXPIRY BREAKDOWN"
                    : "GRN BATCH BREAKDOWN";
            }
        }

        // =========================================================
        // QUANTITY / VALUE SUMMARY
        // =========================================================

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

        public decimal MarkupPercent
        {
            get
            {
                if (TotalCostValue <= 0m)
                    return 0m;

                return Math.Round((PotentialGrossProfit / TotalCostValue) * 100m, 2);
            }
        }

        // For batch-tracked items this should be the real visible batch count.
        // For average-cost items this can be 0 while stock is stored internally
        // in the hidden GENERAL bucket.
        public int BatchCount { get; set; }

        public int StockBucketCount { get; set; }

        public string BatchCountText
        {
            get
            {
                if (!HasBatchTracking)
                    return StockBucketCount > 0 ? "GENERAL" : "-";

                return BatchCount.ToString();
            }
        }

        public bool HasNegativeStock => TotalQtyOnHand < 0m;
        public bool HasZeroStock => TotalQtyOnHand == 0m;
        public bool HasPositiveStock => TotalQtyOnHand > 0m;

        public bool HasExpiredBatch { get; set; }
        public bool HasExpiringSoonBatch { get; set; }

        public string StockStatus { get; set; } = "Normal";

        public DateTime? EarliestExpiryDate { get; set; }
        public DateTime? LastReceivedDate { get; set; }

        // =========================================================
        // DETAIL ROWS
        // =========================================================

        public List<ItemBatchDto> Batches { get; set; } = new();

        public bool HasDetailRows => Batches.Any();

        public string DetailSummaryText
        {
            get
            {
                if (!Batches.Any())
                    return "No stock rows found.";

                if (!HasBatchTracking)
                    return "Stock is stored in the internal GENERAL bucket. Cashier sells by item barcode/SKU.";

                return "Cashier sells by GRN batch barcode for this item.";
            }
        }
    }

    public class ItemBatchDto
    {
        // =========================================================
        // IDENTITY
        // =========================================================

        public int BatchId { get; set; }
        public int ItemVariantId { get; set; }

        public string BatchNo { get; set; } = string.Empty;
        public string InternalBatchBarcode { get; set; } = string.Empty;

        public bool IsGeneralStockBucket { get; set; }
        public bool IsRealBatch => !IsGeneralStockBucket;

        public string RowTypeText => IsGeneralStockBucket ? "GENERAL" : "GRN Batch";

        public string BatchDisplayText
        {
            get
            {
                if (string.IsNullOrWhiteSpace(BatchNo))
                    return "-";

                return BatchNo.Trim();
            }
        }

        public string BarcodeDisplayText
        {
            get
            {
                if (IsGeneralStockBucket)
                    return "-";

                if (string.IsNullOrWhiteSpace(InternalBatchBarcode))
                    return "-";

                return InternalBatchBarcode.Trim();
            }
        }

        // =========================================================
        // DATES
        // =========================================================

        public DateTime? ExpiryDate { get; set; }
        public DateTime ReceivedDate { get; set; }

        public int? DaysToExpire
        {
            get
            {
                if (!ExpiryDate.HasValue)
                    return null;

                return (ExpiryDate.Value.Date - DateTime.Today).Days;
            }
        }

        public string ExpiryStatus
        {
            get
            {
                if (IsGeneralStockBucket)
                    return "Not Applicable";

                if (!ExpiryDate.HasValue)
                    return "No Expiry";

                if (ExpiryDate.Value.Date < DateTime.Today)
                    return "Expired";

                if (ExpiryDate.Value.Date <= DateTime.Today.AddDays(30))
                    return "Expiring Soon";

                return "OK";
            }
        }

        public bool IsExpired =>
            ExpiryDate.HasValue && ExpiryDate.Value.Date < DateTime.Today;

        public bool IsExpiringSoon =>
            ExpiryDate.HasValue &&
            ExpiryDate.Value.Date >= DateTime.Today &&
            ExpiryDate.Value.Date <= DateTime.Today.AddDays(30);

        // =========================================================
        // QTY / VALUE
        // =========================================================

        public decimal CurrentStock { get; set; }

        // Alias for pages that use available quantity wording.
        public decimal AvailableQty => CurrentStock;

        public decimal CostPrice { get; set; }
        public decimal RetailPrice { get; set; }
        public decimal WholesalePrice { get; set; }

        public decimal TotalBatchCost => Math.Round(CurrentStock * CostPrice, 2);
        public decimal TotalBatchRetail => Math.Round(CurrentStock * RetailPrice, 2);
        public decimal TotalBatchWholesale => Math.Round(CurrentStock * WholesalePrice, 2);

        public bool IsDeactivated { get; set; }

        public string StockStatus
        {
            get
            {
                if (CurrentStock < 0m)
                    return "Negative";

                if (CurrentStock == 0m)
                    return "Zero";

                return "In Stock";
            }
        }

        // =========================================================
        // BARCODE PRINT AUDIT
        // =========================================================

        public int BarcodePrintedCount { get; set; }
        public DateTime? LastBarcodePrintedAt { get; set; }
        public string LastBarcodePrintedBy { get; set; } = string.Empty;

        public string PrintedStatusText
        {
            get
            {
                if (IsGeneralStockBucket)
                    return "No batch label";

                if (BarcodePrintedCount <= 0)
                    return "Not printed";

                return $"Printed {BarcodePrintedCount}";
            }
        }
    }
    public static class ExpiryMonitorFilters
    {
        public const string All = "All Expiry-Tracked Stock";
        public const string Expired = "Already Expired";
        public const string Within7Days = "Expiring Within 7 Days";
        public const string Within30Days = "Expiring Within 30 Days";
        public const string Within60Days = "Expiring Within 60 Days";
        public const string Within90Days = "Expiring Within 90 Days";
        public const string MissingExpiry = "No Expiry Date";

        public static IReadOnlyList<string> Values { get; } = new[]
        {
            All,
            Expired,
            Within7Days,
            Within30Days,
            Within60Days,
            Within90Days,
            MissingExpiry
        };
    }

    public class ExpiryMonitorRowDto
    {
        public int BatchId { get; set; }
        public int ParentId { get; set; }
        public int VariantId { get; set; }

        public string ItemCode { get; set; } = string.Empty;
        public string SkuCode { get; set; } = string.Empty;
        public string ItemBarcode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string VariantDescription { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string PrimarySupplierName { get; set; } = string.Empty;
        public string Uom { get; set; } = string.Empty;

        public string BatchNo { get; set; } = string.Empty;
        public string BatchBarcode { get; set; } = string.Empty;

        public DateTime ReceivedDate { get; set; }
        public DateTime? ExpiryDate { get; set; }

        public decimal AvailableQty { get; set; }
        public decimal UnitCost { get; set; }
        public decimal CostValue => Math.Round(AvailableQty * UnitCost, 2);

        public int? DaysRemaining =>
            ExpiryDate.HasValue
                ? (ExpiryDate.Value.Date - DateTime.Today).Days
                : null;

        public bool IsMissingExpiry => !ExpiryDate.HasValue;

        public bool IsExpired =>
            ExpiryDate.HasValue &&
            ExpiryDate.Value.Date < DateTime.Today;

        public bool IsExpiringWithin(int days) =>
            ExpiryDate.HasValue &&
            ExpiryDate.Value.Date >= DateTime.Today &&
            ExpiryDate.Value.Date <= DateTime.Today.AddDays(days);

        public string ExpiryStatus
        {
            get
            {
                if (!ExpiryDate.HasValue)
                    return "No Expiry Date";

                int days = DaysRemaining ?? 0;

                if (days < 0)
                    return "Expired";

                if (days == 0)
                    return "Expires Today";

                if (days <= 7)
                    return "Within 7 Days";

                if (days <= 30)
                    return "Within 30 Days";

                if (days <= 60)
                    return "Within 60 Days";

                if (days <= 90)
                    return "Within 90 Days";

                return "Later";
            }
        }

        public string DaysRemainingText =>
            DaysRemaining.HasValue
                ? DaysRemaining.Value.ToString()
                : "—";

        public string BatchDisplayText =>
            string.IsNullOrWhiteSpace(BatchNo)
                ? "-"
                : BatchNo.Trim();

        public string BatchBarcodeDisplayText =>
            string.IsNullOrWhiteSpace(BatchBarcode)
                ? "-"
                : BatchBarcode.Trim();
    }

}
