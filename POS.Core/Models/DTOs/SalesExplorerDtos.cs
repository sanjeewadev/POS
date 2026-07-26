using System;
using System.Collections.Generic;
using System.Linq;

namespace POS.Core.Models.DTOs
{
    public sealed class PagedSalesResult
    {
        public List<SalesExplorerRecordDto> Records { get; set; } = new();
        public int TotalCount { get; set; }
        public decimal SummaryNetSales { get; set; }
        public decimal SummaryReturns { get; set; }
        public decimal SummaryNetAfterReturns { get; set; }
        public decimal SummaryGrossProfit { get; set; }

        // Compatibility aliases retained for the completed Phase 8D regression path.
        public decimal SummaryTotalRevenue
        {
            get => SummaryNetSales;
            set => SummaryNetSales = value;
        }

        public decimal SummaryTotalProfit
        {
            get => SummaryGrossProfit;
            set => SummaryGrossProfit = value;
        }
    }

    public sealed class SalesExplorerRecordDto
    {
        public int SaleId { get; set; }
        public string InvoiceNo { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; }
        public string TerminalNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CashierName { get; set; } = string.Empty;
        public decimal GrossAmount { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal NetAmount { get; set; }
        public decimal ReturnedAmount { get; set; }
        public decimal NetAfterReturns => NetAmount - ReturnedAmount;
        public decimal TotalCost { get; set; }
        public decimal ReturnedCost { get; set; }
        public decimal GrossProfit => NetAfterReturns - (TotalCost - ReturnedCost);
        public string PaymentMethods { get; set; } = string.Empty;
        public string ReturnStatus { get; set; } = string.Empty;
        public string TaxInvoiceNo { get; set; } = string.Empty;
    }

    public sealed class SaleReceiptDetailsDto
    {
        public int SaleId { get; set; }
        public string InvoiceNo { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; }
        public string TerminalNo { get; set; } = string.Empty;
        public string CashierName { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public string TaxInvoiceNo { get; set; } = string.Empty;
        public decimal GrossAmount { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal GiftVoucherIssueTotal { get; set; }
        public decimal NetAmount { get; set; }
        public decimal ReturnedAmount { get; set; }
        public decimal? TaxableAmountTotal { get; set; }
        public decimal? TotalVatAmount { get; set; }
        public decimal? StandardRatedAmount { get; set; }
        public decimal? ZeroRatedAmount { get; set; }
        public decimal? ExemptAmount { get; set; }
        public decimal? OutOfScopeAmount { get; set; }
        public string TaxSnapshotStatus { get; set; } = string.Empty;
        public string ReturnStatus { get; set; } = string.Empty;
        public List<SaleReceiptLineDto> Lines { get; set; } = new();
        public List<SaleReceiptPaymentDto> Payments { get; set; } = new();
        public List<SalesExplorerCreditNoteDto> CreditNotes { get; set; } = new();
        public List<SalesExplorerDocumentAuditDto> DocumentAudits { get; set; } = new();
    }

    public sealed class SaleReceiptLineDto
    {
        public int SalesLineId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public string BatchNo { get; set; } = string.Empty;
        public string CataloguePriceSource { get; set; } = string.Empty;
        public string CataloguePriceSourceText =>
            CataloguePriceSource == "BatchOverride"
                ? "Batch Override"
                : CataloguePriceSource == "Master"
                    ? "Master Price"
                    : "Legacy / Unknown";
        public decimal Qty { get; set; }
        public decimal ReturnedQty { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal LineTotal { get; set; }
        public string TaxCategory { get; set; } = string.Empty;
        public decimal? TaxRatePercent { get; set; }
        public decimal? TaxableAmount { get; set; }
        public decimal? VatAmount { get; set; }
        public decimal? TaxInclusiveAmount { get; set; }
        public string TaxSnapshotStatus { get; set; } = string.Empty;
        public bool IsFreeItem { get; set; }
        public bool IsGiftVoucherSale { get; set; }
        public string ReturnStatus { get; set; } = string.Empty;
    }

    public sealed class SaleReceiptPaymentDto
    {
        public string PaymentType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal TenderedAmount { get; set; }
        public decimal ChangeAmount { get; set; }
        public string ReferenceNo { get; set; } = string.Empty;
        public string BankOrCardType { get; set; } = string.Empty;
        public string CardLastDigits { get; set; } = string.Empty;
        public DateTime? PaymentDate { get; set; }
        public string EnteredBy { get; set; } = string.Empty;
        public string TerminalNo { get; set; } = string.Empty;
        public string GiftVoucherNo { get; set; } = string.Empty;

        public string Details
        {
            get
            {
                if (PaymentType.Equals("Cash", StringComparison.OrdinalIgnoreCase))
                {
                    decimal tendered = TenderedAmount > 0m ? TenderedAmount : Amount;
                    return ChangeAmount > 0m
                        ? $"Tendered Rs. {tendered:N2} / Change Rs. {ChangeAmount:N2}"
                        : $"Tendered Rs. {tendered:N2}";
                }

                if (PaymentType.Equals("Card", StringComparison.OrdinalIgnoreCase))
                {
                    string masked = string.IsNullOrWhiteSpace(CardLastDigits)
                        ? string.Empty
                        : $"******{CardLastDigits}";
                    return string.Join(" / ", new[] { BankOrCardType, masked, ReferenceNo }
                        .Where(value => !string.IsNullOrWhiteSpace(value)));
                }

                if (PaymentType.Equals("Cheque", StringComparison.OrdinalIgnoreCase))
                {
                    string date = PaymentDate.HasValue ? PaymentDate.Value.ToString("yyyy-MM-dd") : string.Empty;
                    return string.Join(" / ", new[] { ReferenceNo, BankOrCardType, date }
                        .Where(value => !string.IsNullOrWhiteSpace(value)));
                }

                if (PaymentType.Equals("GiftVoucher", StringComparison.OrdinalIgnoreCase) ||
                    PaymentType.Equals("Gift Voucher", StringComparison.OrdinalIgnoreCase))
                {
                    return string.IsNullOrWhiteSpace(GiftVoucherNo) ? ReferenceNo : GiftVoucherNo;
                }

                return ReferenceNo;
            }
        }
    }

    public sealed class SalesExplorerCreditNoteDto
    {
        public int CustomerReturnHeaderId { get; set; }
        public string CreditNoteNo { get; set; } = string.Empty;
        public DateTime ReturnDate { get; set; }
        public decimal RefundAmount { get; set; }
        public string RefundMethod { get; set; } = string.Empty;
        public string AuthorizedBy { get; set; } = string.Empty;
    }

    public sealed class SalesExplorerDocumentAuditDto
    {
        public DateTime OccurredAt { get; set; }
        public string DocumentType { get; set; } = string.Empty;
        public string DocumentNumber { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public int CopyNumber { get; set; }
        public bool IsSuccessful { get; set; }
        public string PerformedBy { get; set; } = string.Empty;
        public string TerminalNo { get; set; } = string.Empty;
        public string PrinterName { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
