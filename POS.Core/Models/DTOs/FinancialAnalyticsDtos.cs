using System;

namespace POS.Core.Models.DTOs
{
    // ==============================================================================
    // 1. MACRO FINANCIAL SUMMARY (For Top KPI Cards)
    // ==============================================================================
    public class FinancialSummaryDto
    {
        public decimal GrossSales { get; set; }
        public decimal TotalDiscounts { get; set; }
        public decimal TotalReturns { get; set; }

        // Net Sales = All money coming in, minus discounts and refunds given back
        public decimal NetSales => GrossSales - TotalDiscounts - TotalReturns;

        public decimal TotalCostOfGoods { get; set; }

        // Gross Profit = What you made after paying for the physical items
        public decimal GrossProfit => NetSales - TotalCostOfGoods;

        // Operating Expenses = Store payouts (Petty cash, cleaning supplies, etc.)
        public decimal OperatingExpenses { get; set; }

        // Operating Margin = True store profitability after daily expenses
        public decimal OperatingMargin => GrossProfit - OperatingExpenses;

        public int TotalSalesCount { get; set; }
        public decimal AverageSaleValue => TotalSalesCount == 0 ? 0 : NetSales / TotalSalesCount;
    }

    // ==============================================================================
    // 2. TIME-SERIES TREND DATA (For LiveCharts)
    // ==============================================================================
    public class FinancialTrendPointDto
    {
        public DateTime Date { get; set; }
        public string DateLabel { get; set; } = string.Empty;

        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
        public decimal Profit => Revenue - Cost;

        public int TransactionCount { get; set; }
    }
}