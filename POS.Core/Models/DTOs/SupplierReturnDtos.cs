using System;
using System.Collections.Generic;
using POS.Core.Models;

namespace POS.Core.Models.DTOs
{
    public sealed class SupplierReturnPostResult
    {
        public SupplierReturnHeader ReturnHeader { get; init; } = null!;
        public SupplierDebitNoteDto DebitNote { get; init; } = null!;
    }

    public sealed class SupplierDebitNoteDto
    {
        public string StoreName { get; init; } = string.Empty;
        public string StoreAddress { get; init; } = string.Empty;
        public string StorePhone { get; init; } = string.Empty;
        public string StoreTin { get; init; } = string.Empty;
        public string StoreVatNo { get; init; } = string.Empty;

        public string DebitNoteNumber { get; init; } = string.Empty;
        public DateTime ReturnDate { get; init; }
        public string SupplierName { get; init; } = string.Empty;
        public string SupplierCode { get; init; } = string.Empty;
        public string SupplierVatNo { get; init; } = string.Empty;
        public string SupplierAddress { get; init; } = string.Empty;
        public string GrnNumber { get; init; } = string.Empty;
        public string OriginalSupplierInvoiceNo { get; init; } = string.Empty;
        public string AuthorizedBy { get; init; } = string.Empty;
        public string Remarks { get; init; } = string.Empty;

        public decimal GrossCredit { get; init; }
        public decimal NetCredit { get; init; }
        public decimal? TaxableAmountTotal { get; init; }
        public decimal? TotalVatAmount { get; init; }
        public decimal? StandardRatedAmount { get; init; }
        public decimal? ZeroRatedAmount { get; init; }
        public decimal? ExemptAmount { get; init; }
        public decimal? OutOfScopeAmount { get; init; }
        public string TaxSnapshotStatus { get; init; } = string.Empty;

        public IReadOnlyList<SupplierDebitNoteLineDto> Lines { get; init; } = Array.Empty<SupplierDebitNoteLineDto>();
    }

    public sealed class SupplierDebitNoteLineDto
    {
        public string ItemCode { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string BatchNo { get; init; } = string.Empty;
        public decimal ReturnQuantity { get; init; }
        public decimal HistoricalLandedCost { get; init; }
        public decimal SupplierCredit { get; init; }
        public string TaxCategoryCode { get; init; } = string.Empty;
        public string TaxName { get; init; } = string.Empty;
        public decimal? TaxRatePercent { get; init; }
        public decimal? TaxableAmount { get; init; }
        public decimal? VatAmount { get; init; }
        public decimal? TaxInclusiveAmount { get; init; }
        public string TaxSnapshotStatus { get; init; } = string.Empty;
        public string ReasonCode { get; init; } = string.Empty;
        public string Remarks { get; init; } = string.Empty;
    }
}
