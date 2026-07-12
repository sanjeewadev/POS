using System;
using System.Collections.Generic;

namespace POS.Core.Models.DTOs
{
    public sealed class VatReportResultDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public VatReportSummaryDto Summary { get; set; } = new();
        public List<VatReportDocumentRowDto> Documents { get; set; } = new();
        public List<VatCategoryRateRowDto> CategoryRateRows { get; set; } = new();
        public List<VatLegacyUnknownRowDto> LegacyUnknownRows { get; set; } = new();
        public List<VatReconciliationRowDto> ReconciliationRows { get; set; } = new();
    }

    public sealed class VatReportSummaryDto
    {
        public string StoreName { get; set; } = "Store";
        public bool IsCurrentlyVatRegistered { get; set; }
        public string VatRegistrationNumber { get; set; } = string.Empty;

        public decimal GrossSalesTaxableAmount { get; set; }
        public decimal CustomerReturnTaxableAmount { get; set; }
        public decimal NetSalesTaxableAmount { get; set; }
        public decimal GrossOutputVat { get; set; }
        public decimal CustomerReturnVat { get; set; }
        public decimal NetOutputVat { get; set; }

        public decimal GrossPurchaseTaxableAmount { get; set; }
        public decimal SupplierReturnTaxableAmount { get; set; }
        public decimal NetPurchaseTaxableAmount { get; set; }
        public decimal GrossInputVat { get; set; }
        public decimal SupplierReturnVat { get; set; }
        public decimal NetInputVat { get; set; }

        public decimal OperationalVatPosition { get; set; }
        public int CompleteDocumentCount { get; set; }
        public int LegacyUnknownDocumentCount { get; set; }
        public int DiscrepancyCount { get; set; }

        public decimal SalesZeroRatedAmount { get; set; }
        public decimal SalesExemptAmount { get; set; }
        public decimal SalesOutOfScopeAmount { get; set; }
        public decimal PurchaseZeroRatedAmount { get; set; }
        public decimal PurchaseExemptAmount { get; set; }
        public decimal PurchaseOutOfScopeAmount { get; set; }

        public string CurrentRegistrationText =>
            IsCurrentlyVatRegistered
                ? string.IsNullOrWhiteSpace(VatRegistrationNumber)
                    ? "Currently VAT registered"
                    : $"Currently VAT registered — VAT No. {VatRegistrationNumber}"
                : "Currently not VAT registered";

        public string PositionLabel =>
            OperationalVatPosition >= 0m
                ? "Output VAT above input VAT"
                : "Input VAT above output VAT";
    }

    public sealed class VatReportDocumentRowDto
    {
        public string SourceType { get; set; } = string.Empty;
        public string DocumentNumber { get; set; } = string.Empty;
        public string RelatedDocumentNumber { get; set; } = string.Empty;
        public DateTime DocumentDate { get; set; }
        public bool IsReversal { get; set; }
        public decimal TaxableAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal InclusiveAmount { get; set; }
        public decimal StandardRatedAmount { get; set; }
        public decimal ZeroRatedAmount { get; set; }
        public decimal ExemptAmount { get; set; }
        public decimal OutOfScopeAmount { get; set; }
        public string SnapshotStatus { get; set; } = string.Empty;

        public decimal NetTaxableEffect => IsReversal ? -TaxableAmount : TaxableAmount;
        public decimal NetVatEffect => IsReversal ? -VatAmount : VatAmount;
        public string Direction => IsReversal ? "Reversal" : "Original";
    }

    public sealed class VatCategoryRateRowDto
    {
        public string SourceType { get; set; } = string.Empty;
        public string TaxCategoryCode { get; set; } = string.Empty;
        public decimal? VatRatePercent { get; set; }
        public bool IsReversal { get; set; }
        public int DocumentCount { get; set; }
        public decimal TaxableAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal InclusiveAmount { get; set; }

        public decimal NetTaxableEffect => IsReversal ? -TaxableAmount : TaxableAmount;
        public decimal NetVatEffect => IsReversal ? -VatAmount : VatAmount;
        public string VatRateDisplay => VatRatePercent.HasValue
            ? $"{VatRatePercent.Value:N4}%"
            : "Unknown";
    }

    public sealed class VatLegacyUnknownRowDto
    {
        public string SourceType { get; set; } = string.Empty;
        public string DocumentNumber { get; set; } = string.Empty;
        public string RelatedDocumentNumber { get; set; } = string.Empty;
        public DateTime DocumentDate { get; set; }
        public decimal FinancialAmount { get; set; }
        public string SnapshotStatus { get; set; } = string.Empty;
        public string Warning { get; set; } = string.Empty;
    }

    public sealed class VatReconciliationRowDto
    {
        public string SourceType { get; set; } = string.Empty;
        public string DocumentNumber { get; set; } = string.Empty;
        public DateTime DocumentDate { get; set; }
        public string FieldChecked { get; set; } = string.Empty;
        public decimal HeaderOrSourceAmount { get; set; }
        public decimal DetailOrReturnedAmount { get; set; }
        public decimal Difference { get; set; }
        public string Explanation { get; set; } = string.Empty;
    }
}
