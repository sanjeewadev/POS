using System;
using System.Collections.Generic;

namespace POS.Core.Models.DTOs
{
    public sealed class FinancialSummaryDto
    {
        public decimal GrossMerchandiseSales { get; set; }
        public decimal TotalDiscounts { get; set; }
        public decimal MerchandiseSalesAfterDiscounts => GrossMerchandiseSales - TotalDiscounts;
        public decimal CustomerReturns { get; set; }
        public decimal NetSales => MerchandiseSalesAfterDiscounts - CustomerReturns;
        public decimal SaleCostOfGoods { get; set; }
        public decimal ReturnedCostOfGoods { get; set; }
        public decimal NetCostOfGoods => SaleCostOfGoods - ReturnedCostOfGoods;
        public decimal GrossProfit => NetSales - NetCostOfGoods;
        public decimal GiftVoucherIssueValue { get; set; }
        public decimal PostedPurchases { get; set; }
        public decimal PostedSupplierReturns { get; set; }
        public decimal PaidIn { get; set; }
        public decimal PaidOut { get; set; }
        public decimal FloatIn { get; set; }
        public decimal FloatOut { get; set; }
        public decimal CustomerCashRefunds { get; set; }
        public int TotalSalesCount { get; set; }
        public decimal AverageSaleValue => TotalSalesCount == 0 ? 0m : NetSales / TotalSalesCount;
        public List<FinancialTenderTotalDto> TenderTotals { get; set; } = new();
    }

    public sealed class FinancialTenderTotalDto
    {
        public string PaymentType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int TransactionCount { get; set; }
    }
}
