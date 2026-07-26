using System;

namespace POS.Core.Models.DTOs
{
    public sealed class PriceChangeOperationSummaryDto
    {
        public string OperationKey { get; set; } = string.Empty;
        public string PriceChangeNo { get; set; } = string.Empty;
        public DateTime ChangedAt { get; set; }
        public string ChangedBy { get; set; } = string.Empty;
        public string ChangeSource { get; set; } = string.Empty;
        public string ItemVariantSummary { get; set; } = string.Empty;
        public string MasterChangeSummary { get; set; } = "No master change";
        public int BatchOverrideCount { get; set; }
        public int DetailRowCount { get; set; }
        public int AffectedVariantCount { get; set; }
        public string SourceDocumentNo { get; set; } = string.Empty;
        public string ChangeReason { get; set; } = string.Empty;
        public string BatchOverrideSummary => BatchOverrideCount.ToString();
        public string DisplayChangeNo => string.IsNullOrWhiteSpace(PriceChangeNo)
            ? "Legacy"
            : PriceChangeNo;
    }

    public sealed class PriceChangeDetailDto
    {
        public int Id { get; set; }
        public string PriceChangeNo { get; set; } = string.Empty;
        public DateTime ChangedAt { get; set; }
        public string ChangedBy { get; set; } = string.Empty;
        public string PriceLevel { get; set; } = string.Empty;
        public string ChangeAction { get; set; } = string.Empty;
        public string ChangeSource { get; set; } = string.Empty;
        public string OldPriceSource { get; set; } = string.Empty;
        public string NewPriceSource { get; set; } = string.Empty;
        public int ItemVariantId { get; set; }
        public int? ItemBatchId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string SkuCode { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string ItemDescription { get; set; } = string.Empty;
        public string VariantDescription { get; set; } = string.Empty;
        public string BatchNo { get; set; } = string.Empty;
        public DateTime? BatchExpiryDate { get; set; }
        public decimal EffectiveCost { get; set; }
        public decimal OldMinimumPrice { get; set; }
        public decimal NewMinimumPrice { get; set; }
        public decimal OldRetailPrice { get; set; }
        public decimal NewRetailPrice { get; set; }
        public decimal OldWholesalePrice { get; set; }
        public decimal NewWholesalePrice { get; set; }
        public decimal OldMaximumPrice { get; set; }
        public decimal NewMaximumPrice { get; set; }
        public string SourceDocumentType { get; set; } = string.Empty;
        public string SourceDocumentNo { get; set; } = string.Empty;
        public string ChangeReason { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;

        public string ItemVariantDisplay =>
            string.IsNullOrWhiteSpace(VariantDescription) ||
            VariantDescription.Equals("Standard", StringComparison.OrdinalIgnoreCase)
                ? ItemDescription
                : $"{ItemDescription} - {VariantDescription}";

        public string PriceSourceTransition =>
            string.Equals(OldPriceSource, NewPriceSource, StringComparison.Ordinal)
                ? OldPriceSource
                : $"{OldPriceSource} → {NewPriceSource}";
    }
}
