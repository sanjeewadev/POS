using System;
using System.Collections.Generic;
using POS.Core.Models;

namespace POS.Core.Models.DTOs
{
    public sealed class CustomerReturnInvoiceDto
    {
        public int SalesHeaderId { get; init; }
        public string InvoiceNo { get; init; } = string.Empty;
        public DateTime TransactionDate { get; init; }
        public string CustomerName { get; init; } = string.Empty;
        public string TerminalNo { get; init; } = string.Empty;
        public string CashierName { get; init; } = string.Empty;
        public string TaxSnapshotStatus { get; init; } = string.Empty;
        public decimal NetTotal { get; init; }
        public List<CustomerReturnableLineDto> Lines { get; init; } = new();
    }

    public sealed class CustomerReturnableLineDto
    {
        public int SalesLineId { get; init; }
        public int ItemVariantId { get; init; }
        public int? ItemBatchId { get; init; }
        public string ItemType { get; init; } = string.Empty;
        public string ItemDescription { get; init; } = string.Empty;
        public string SkuCode { get; init; } = string.Empty;
        public string BatchNo { get; init; } = string.Empty;
        public string Uom { get; init; } = string.Empty;
        public decimal SoldQuantity { get; init; }
        public decimal PreviouslyReturnedQuantity { get; init; }
        public decimal RemainingQuantity { get; init; }
        public decimal UnitPrice { get; init; }
        public decimal OriginalGrossAmount { get; init; }
        public decimal OriginalDiscountAmount { get; init; }
        public decimal OriginalRefundAmount { get; init; }
        public decimal PreviouslyRefundedAmount { get; init; }
        public decimal? OriginalTaxableAmount { get; init; }
        public decimal? OriginalVatAmount { get; init; }
        public decimal? OriginalTaxInclusiveAmount { get; init; }
        public decimal PreviouslyReturnedTaxableAmount { get; init; }
        public decimal PreviouslyReturnedVatAmount { get; init; }
        public decimal PreviouslyReturnedTaxInclusiveAmount { get; init; }
        public int? TaxCategoryId { get; init; }
        public int? TaxRateId { get; init; }
        public string? TaxCategoryCodeSnapshot { get; init; }
        public string? TaxCodeSnapshot { get; init; }
        public string? TaxNameSnapshot { get; init; }
        public decimal? TaxRatePercentSnapshot { get; init; }
        public bool? IsTaxInclusiveSnapshot { get; init; }
        public string TaxSnapshotStatus { get; init; } = string.Empty;
        public bool IsReturnable { get; init; }
        public string BlockReason { get; init; } = string.Empty;
    }

    public sealed class CustomerReturnRequest
    {
        public int SalesHeaderId { get; set; }
        public int ShiftSessionId { get; set; }
        public string TerminalNo { get; set; } = string.Empty;
        public string CashierName { get; set; } = string.Empty;
        public string AuthorizedBy { get; set; } = string.Empty;
        public string ReturnReason { get; set; } = string.Empty;
        public List<CustomerReturnRequestLine> Lines { get; set; } = new();
    }

    public sealed class CustomerReturnRequestLine
    {
        public int SalesLineId { get; set; }
        public decimal Quantity { get; set; }
    }

    public sealed class CustomerReturnProcessResult
    {
        public CustomerReturnHeader ReturnHeader { get; init; } = null!;
        public string CreditNoteNo { get; init; } = string.Empty;
        public decimal TotalRefundAmount { get; init; }
    }

    public sealed class CustomerReturnAllocationInput
    {
        public decimal SoldQuantity { get; init; }
        public decimal PreviouslyReturnedQuantity { get; init; }
        public decimal RequestedQuantity { get; init; }
        public decimal OriginalGrossAmount { get; init; }
        public decimal OriginalDiscountAmount { get; init; }
        public decimal OriginalRefundAmount { get; init; }
        public decimal PreviouslyRefundedAmount { get; init; }
        public decimal? OriginalTaxableAmount { get; init; }
        public decimal? OriginalVatAmount { get; init; }
        public decimal? OriginalTaxInclusiveAmount { get; init; }
        public decimal PreviouslyReturnedTaxableAmount { get; init; }
        public decimal PreviouslyReturnedVatAmount { get; init; }
        public decimal PreviouslyReturnedTaxInclusiveAmount { get; init; }
        public string TaxSnapshotStatus { get; init; } = string.Empty;
    }

    public sealed class CustomerReturnAllocationResult
    {
        public decimal Quantity { get; init; }
        public decimal GrossAmount { get; init; }
        public decimal DiscountAmount { get; init; }
        public decimal RefundAmount { get; init; }
        public decimal RefundUnitValue { get; init; }
        public decimal? TaxableAmount { get; init; }
        public decimal? VatAmount { get; init; }
        public decimal? TaxInclusiveAmount { get; init; }
        public bool IsFinalQuantity { get; init; }
    }
}
