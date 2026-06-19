using System;
using System.Collections.Generic;

namespace POS.Core.Models.DTOs
{
    // ==============================================================================
    // 1. PAGINATION WRAPPER (Critical for 100,000+ record performance)
    // ==============================================================================
    public class PagedSalesResult
    {
        public List<SalesExplorerRecordDto> Records { get; set; } = new();
        public int TotalCount { get; set; }

        // We calculate these on the server so the UI doesn't have to download 100k rows just to show the sum
        public decimal SummaryTotalRevenue { get; set; }
        public decimal SummaryTotalProfit { get; set; }
    }

    // ==============================================================================
    // 2. THE GRID ROW (Lightweight object for the master table)
    // ==============================================================================
    public class SalesExplorerRecordDto
    {
        public int SaleId { get; set; }
        public string InvoiceNo { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CashierName { get; set; } = string.Empty;

        public decimal GrossAmount { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal NetAmount { get; set; }

        public decimal TotalCost { get; set; }
        public decimal Profit => NetAmount - TotalCost;

        // We will combine the 1-to-Many payments into a single string (e.g., "Cash, Card") for the grid
        public string PaymentMethods { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;
    }

    // ==============================================================================
    // 3. THE DRILL-DOWN RECEIPT (Heavy object, only loaded when a row is double-clicked)
    // ==============================================================================
    public class SaleReceiptDetailsDto
    {
        public int SaleId { get; set; }
        public string InvoiceNo { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; }
        public string TerminalNo { get; set; } = string.Empty;
        public string CashierName { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;

        public decimal GrossAmount { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal NetAmount { get; set; }

        public List<SaleReceiptLineDto> Lines { get; set; } = new();
        public List<SaleReceiptPaymentDto> Payments { get; set; } = new();
    }

    public class SaleReceiptLineDto
    {
        public string ItemCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Qty { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class SaleReceiptPaymentDto
    {
        public string PaymentType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string ReferenceNo { get; set; } = string.Empty;
    }
}