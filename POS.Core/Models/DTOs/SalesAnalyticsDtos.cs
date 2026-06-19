using System;

namespace POS.Core.Models.DTOs
{
    public class AnalyticsKpiDto
    {
        public decimal TotalRevenue { get; set; }
        public decimal TotalCost { get; set; }
        public decimal GrossProfit => TotalRevenue - TotalCost;
        public double AverageMargin => TotalRevenue == 0 ? 0 : Math.Round((double)(GrossProfit / TotalRevenue) * 100, 2);

        public decimal TotalUnitsSold { get; set; }
        public int ActiveSellingItems { get; set; }
        public int DeadStockItems { get; set; }
    }

    public class ItemPerformanceDto
    {
        public int Rank { get; set; }
        public int ItemId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public decimal CurrentStock { get; set; }

        public decimal QtySold { get; set; }
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
        public decimal Profit => Revenue - Cost;
        public double Margin => Revenue == 0 ? 0 : Math.Round((double)(Profit / Revenue) * 100, 2);

        public DateTime? LastSoldDate { get; set; }
        public int DaysSinceLastSale => LastSoldDate.HasValue ? (DateTime.Today - LastSoldDate.Value.Date).Days : 999;
        public double AverageDailySales { get; set; }
    }

    public class TrendPointDto
    {
        public string DateLabel { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public decimal Profit { get; set; }
    }

    public class ItemDrillDownDto
    {
        public DateTime TransactionDate { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public string DocumentNo { get; set; } = string.Empty;
        public string PartyName { get; set; } = string.Empty;
        public decimal QtyIn { get; set; }
        public decimal QtyOut { get; set; }
    }
}