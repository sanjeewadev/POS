using System;
using System.Collections.Generic;

namespace POS.Core.Models.DTOs
{
    public sealed class PdfTableColumnDto
    {
        public string Header { get; init; } = string.Empty;
        public double WidthCentimeters { get; init; } = 2.5d;
        public bool IsNumeric { get; init; }
    }

    public sealed class PdfTableDocumentDto
    {
        public string Title { get; init; } = string.Empty;
        public string Subtitle { get; init; } = string.Empty;
        public string StoreHeading { get; init; } = string.Empty;
        public string GeneratedBy { get; init; } = string.Empty;
        public DateTime GeneratedAt { get; init; } = DateTime.Now;
        public string FooterText { get; init; } = string.Empty;
        public IReadOnlyList<string> SummaryLines { get; init; } = Array.Empty<string>();
        public IReadOnlyList<PdfTableColumnDto> Columns { get; init; } = Array.Empty<PdfTableColumnDto>();
        public IReadOnlyList<IReadOnlyList<string?>> Rows { get; init; } = Array.Empty<IReadOnlyList<string?>>();

        // In POS.Core\Models\DTOs\PdfTableDocumentDto.cs (or wherever your PdfTableDocumentDto is defined)

        public IReadOnlyList<string>? PostTableSummaryLines { get; set; }

    }

    public sealed class DashboardSummaryDto
    {
        public DateTime StartDate { get; init; }
        public DateTime EndDate { get; init; }
        public decimal MerchandiseSales { get; init; }
        public decimal CustomerReturns { get; init; }
        public decimal NetSales { get; init; }
        public decimal GrossProfit { get; init; }
        public decimal CustomerCreditOutstanding { get; init; }
        public decimal SupplierOutstanding { get; init; }
        public int LowStockCount { get; init; }
        public int NegativeStockCount { get; init; }
        public int OpenShiftCount { get; init; }
        public int HeldCartCount { get; init; }
        public int DraftSupplierClaimCount { get; init; }
        public int SubmittedSupplierClaimCount { get; init; }
        public IReadOnlyList<FinancialTenderTotalDto> TenderTotals { get; init; } = Array.Empty<FinancialTenderTotalDto>();
        public IReadOnlyList<DashboardTopItemDto> TopItems { get; init; } = Array.Empty<DashboardTopItemDto>();
        public IReadOnlyList<DashboardAttentionDto> AttentionItems { get; init; } = Array.Empty<DashboardAttentionDto>();
    }

    public sealed class DashboardTopItemDto
    {
        public int Rank { get; init; }
        public string ItemCode { get; init; } = string.Empty;
        public string ItemName { get; init; } = string.Empty;
        public string ItemType { get; init; } = string.Empty;
        public decimal NetQuantity { get; init; }
        public decimal NetSales { get; init; }
    }

    public sealed class DashboardAttentionDto
    {
        public string Severity { get; init; } = "Information";
        public string Area { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public int Count { get; init; }
    }

    public sealed class CustomerPaymentReceiptDocumentDto
    {
        public string StoreName { get; init; } = string.Empty;
        public string StoreAddress { get; init; } = string.Empty;
        public string StorePhone { get; init; } = string.Empty;
        public string ReceiptNo { get; init; } = string.Empty;
        public DateTime PaymentDate { get; init; }
        public string CustomerCode { get; init; } = string.Empty;
        public string CustomerName { get; init; } = string.Empty;
        public string PaymentMethod { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public string ReferenceNo { get; init; } = string.Empty;
        public string BankOrCardType { get; init; } = string.Empty;
        public string DestinationAccount { get; init; } = string.Empty;
        public string ProcessedBy { get; init; } = string.Empty;
        public string TerminalNo { get; init; } = string.Empty;
        public string Remarks { get; init; } = string.Empty;
        public decimal RemainingBalance { get; init; }
        public IReadOnlyList<CustomerLedgerAllocationDto> Allocations { get; init; } = Array.Empty<CustomerLedgerAllocationDto>();
    }
}
