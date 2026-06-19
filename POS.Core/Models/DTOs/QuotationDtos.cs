using System;
using System.Collections.Generic;

namespace POS.Core.DTOs
{
    // ==============================================================================
    // 1. LIGHTWEIGHT GRID OBJECT (For searching and filtering quotes instantly)
    // ==============================================================================
    public class QuotationGridDto
    {
        public int QuotationId { get; set; }
        public string QuoteNo { get; set; } = string.Empty;
        public DateTime DateCreated { get; set; }
        public DateTime ValidUntil { get; set; }

        public string CustomerName { get; set; } = string.Empty;
        public string CashierName { get; set; } = string.Empty;

        public decimal NetTotal { get; set; }
        public string Status { get; set; } = string.Empty;

        // UI Helper: If the quote is past its ValidUntil date, flag it as expired visually
        public bool IsExpired => Status != "Accepted" && Status != "Converted" && DateTime.Today > ValidUntil.Date;
    }

    // ==============================================================================
    // 2. HEAVYWEGHT DETAIL OBJECT (For the Builder, Printing, and Converting to Sale)
    // ==============================================================================
    public class QuotationDetailDto
    {
        public int QuotationId { get; set; }
        public string QuoteNo { get; set; } = string.Empty;
        public DateTime DateCreated { get; set; } = DateTime.Now;
        public DateTime ValidUntil { get; set; } = DateTime.Now.AddDays(7); // Standard 7-day quote validity
        public string Status { get; set; } = "Draft";

        // --- Customer Context ---
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;

        // --- System Context ---
        public string CashierName { get; set; } = string.Empty;
        public string TerminalNo { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;

        // --- Math Context ---
        public decimal GrossTotal { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal NetTotal { get; set; }

        public List<QuotationLineDto> Lines { get; set; } = new();
    }

    // ==============================================================================
    // 3. THE QUOTATION LINE (Items promised to the customer)
    // ==============================================================================
    public class QuotationLineDto
    {
        public string ItemCode { get; set; } = string.Empty;
        public string ItemDescription { get; set; } = string.Empty;

        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountAmount { get; set; }

        public decimal LineTotal => (Quantity * UnitPrice) - DiscountAmount;

        // Critical for the manager to see if the quoted price will still yield a profit
        public decimal CostPrice { get; set; }
        public decimal EstimatedProfit => LineTotal - (Quantity * CostPrice);
    }
}