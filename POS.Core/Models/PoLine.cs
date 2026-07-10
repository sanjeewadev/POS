using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class PoLine
    {
        public int Id { get; set; }

        public int PoHeaderId { get; set; }

        public PoHeader PoHeader { get; set; } = null!;

        [Required]
        public int ItemVariantId { get; set; }

        public ItemVariant ItemVariant { get; set; } = null!;

        // =========================================================
        // UI HELPERS - NOT SAVED
        // =========================================================

        [NotMapped]
        public string ItemCode { get; set; } = string.Empty;

        [NotMapped]
        public string SkuCode { get; set; } = string.Empty;

        [NotMapped]
        public string VariantDescription { get; set; } = string.Empty;

        [NotMapped]
        public string Description { get; set; } = string.Empty;

        [NotMapped]
        public string PrintName { get; set; } = string.Empty;

        [NotMapped]
        public string Barcode { get; set; } = string.Empty;

        [NotMapped]
        public bool HasBatchTracking { get; set; }

        [NotMapped]
        public bool HasExpiryTracking { get; set; }

        [NotMapped]
        public string TrackingText
        {
            get
            {
                if (!HasBatchTracking)
                    return "Average Cost";

                return HasExpiryTracking ? "Batch + Expiry" : "Batch";
            }
        }


        [NotMapped]
        public decimal SOH { get; set; } = 0m;

        [NotMapped]
        public int Moq { get; set; } = 1;

        [NotMapped]
        public decimal RemainingQty => OrderQty - ReceivedQty < 0
            ? 0m
            : OrderQty - ReceivedQty;

        [NotMapped]
        public bool IsFullyReceived => OrderQty > 0 && ReceivedQty >= OrderQty;

        [NotMapped]
        public string FullDisplayName =>
            BuildDisplayName(Description, VariantDescription, fallback: ItemCode);

        [NotMapped]
        public string ReceiptDisplayName =>
            BuildDisplayName(
                string.IsNullOrWhiteSpace(PrintName) ? Description : PrintName,
                VariantDescription,
                fallback: FullDisplayName);

        [NotMapped]
        public string DisplayName => FullDisplayName;

        [NotMapped]
        public string VariantDisplayName =>
            IsStandardVariant(VariantDescription) ? "Standard" : NormalizeText(VariantDescription);

        [NotMapped]
        public decimal GrossAmount => Math.Round(OrderQty * ExpectedCost, 2);

        [NotMapped]
        public decimal VatAmount
        {
            get => TaxAmount;
            set => TaxAmount = value;
        }

        // =========================================================
        // SAVED LINE DATA
        // =========================================================

        [MaxLength(20)]
        public string Uom { get; set; } = string.Empty;

        // Kept for old database compatibility.
        // New Item Master UI does not need to show this as "Vendor SKU".
        [MaxLength(100)]
        public string SupplierItemCode { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,3)")]
        public decimal OrderQty { get; set; } = 0m;

        // Updated by GRN receiving, not by PO editing.
        [Column(TypeName = "decimal(18,3)")]
        public decimal ReceivedQty { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal ExpectedCost { get; set; } = 0m;

        // Amount / Percent.
        [MaxLength(20)]
        public string LineDiscountMode { get; set; } = "Amount";

        // User-entered value.
        // Example:
        // Mode Amount  -> 500
        // Mode Percent -> 10
        [Column(TypeName = "decimal(18,2)")]
        public decimal LineDiscountValue { get; set; } = 0m;

        // Final calculated discount amount.
        // Keep this because old code already uses LineDiscount.
        [Column(TypeName = "decimal(18,2)")]
        public decimal LineDiscount { get; set; } = 0m;

        // Product VAT only. No income tax/accounting tax.
        [MaxLength(20)]
        public string TaxCode { get; set; } = "VAT";

        [Column(TypeName = "decimal(5,2)")]
        public decimal VatRatePercent { get; set; } = 0m;

        // False = VAT added on top.
        // True = ExpectedCost already includes VAT.
        public bool IsVatIncluded { get; set; } = false;

        // Existing field name kept for compatibility.
        // This is product VAT amount.
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; } = 0m;

        // Open, Closed, Cancelled.
        // We will remove Partially Received behavior from the user workflow.
        [MaxLength(30)]
        public string LineStatus { get; set; } = "Open";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public DateTime? ClosedAt { get; set; }

        private static string BuildDisplayName(
            string? baseName,
            string? variantDescription,
            string? fallback)
        {
            string cleanBaseName = NormalizeText(baseName);
            string cleanVariant = NormalizeText(variantDescription);
            string cleanFallback = NormalizeText(fallback);

            if (IsStandardVariant(cleanVariant))
            {
                if (!string.IsNullOrWhiteSpace(cleanBaseName))
                    return cleanBaseName;

                return cleanFallback;
            }

            if (string.IsNullOrWhiteSpace(cleanBaseName))
                return cleanVariant;

            return $"{cleanBaseName} - {cleanVariant}";
        }

        private static bool IsStandardVariant(string? value)
        {
            string cleanValue = NormalizeText(value);

            return string.IsNullOrWhiteSpace(cleanValue) ||
                   cleanValue.Equals("Standard", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}