using System;
using System.Collections.Generic;
using POS.Core.Utilities;

namespace POS.Core.Models.DTOs
{
    public sealed class AnalyticsKpiDto
    {
        public decimal GrossSales { get; set; }
        public decimal Discounts { get; set; }
        public decimal ReturnValue { get; set; }
        public decimal NetSales { get; set; }
        public decimal NetCost { get; set; }
        public decimal GrossProfit => NetSales - NetCost;
        public decimal SoldQuantity { get; set; }
        public decimal ReturnedQuantity { get; set; }
        public decimal NetQuantity => SoldQuantity - ReturnedQuantity;
        public int SellingItemCount { get; set; }
        public int SlowOrNonSellingStockItemCount { get; set; }
    }

    public sealed class ItemPerformanceDto
    {
        public int Rank { get; set; }
        public int ItemVariantId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string SkuCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public decimal? CurrentStock { get; set; }
        public decimal SoldQuantity { get; set; }
        public decimal ReturnedQuantity { get; set; }
        public decimal NetQuantity => SoldQuantity - ReturnedQuantity;
        public decimal GrossSales { get; set; }
        public decimal Discounts { get; set; }
        public decimal ReturnValue { get; set; }
        public decimal NetSales => GrossSales - Discounts - ReturnValue;
        public decimal SaleCost { get; set; }
        public decimal ReturnedCost { get; set; }
        public decimal NetCost => SaleCost - ReturnedCost;
        public decimal GrossProfit => NetSales - NetCost;
        public decimal MarginPercent => NetSales == 0m
            ? 0m
            : Math.Round(GrossProfit / NetSales * 100m, 2);
        public DateTime? LastSaleDate { get; set; }
        public bool IsSlowOrNonSelling { get; set; }
        public string StockDisplay => CurrentStock.HasValue ? QuantityDisplayFormatter.Format(CurrentStock.Value) : "N/A";
    }

    public sealed class ItemSalesTransactionDto
    {
        public DateTime TransactionDate { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public string DocumentNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal Value { get; set; }
    }

    public sealed class ItemSalesAnalyticsResultDto
    {
        public AnalyticsKpiDto Summary { get; set; } = new();
        public List<ItemPerformanceDto> Items { get; set; } = new();
    }
}
