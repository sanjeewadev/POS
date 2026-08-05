using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using POS.Core.Configuration;
using CommunityToolkit.Mvvm.ComponentModel;

namespace POS.Core.Models
{
    public partial class PoLine : ObservableValidator
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

        private string _itemCode = string.Empty;
        [NotMapped]
        public string ItemCode
        {
            get => _itemCode;
            set => SetProperty(ref _itemCode, value);
        }

        private string _skuCode = string.Empty;
        [NotMapped]
        public string SkuCode
        {
            get => _skuCode;
            set => SetProperty(ref _skuCode, value);
        }

        private string _variantDescription = string.Empty;
        [NotMapped]
        public string VariantDescription
        {
            get => _variantDescription;
            set => SetProperty(ref _variantDescription, value);
        }

        private string _description = string.Empty;
        [NotMapped]
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        private string _printName = string.Empty;
        [NotMapped]
        public string PrintName
        {
            get => _printName;
            set => SetProperty(ref _printName, value);
        }

        private string _barcode = string.Empty;
        [NotMapped]
        public string Barcode
        {
            get => _barcode;
            set => SetProperty(ref _barcode, value);
        }

        private bool _hasBatchTracking;
        [NotMapped]
        public bool HasBatchTracking
        {
            get => _hasBatchTracking;
            set => SetProperty(ref _hasBatchTracking, value);
        }

        private bool _hasExpiryTracking;
        [NotMapped]
        public bool HasExpiryTracking
        {
            get => _hasExpiryTracking;
            set => SetProperty(ref _hasExpiryTracking, value);
        }

        [NotMapped]
        public string TrackingText
        {
            get
            {
                if (!_hasBatchTracking)
                    return "Average Cost";

                return _hasExpiryTracking ? "Batch + Expiry" : "Batch";
            }
        }


        private decimal _soh = 0m;
        [NotMapped]
        public decimal SOH
        {
            get => _soh;
            set => SetProperty(ref _soh, value);
        }

        private int _moq = 1;
        [NotMapped]
        public int Moq
        {
            get => _moq;
            set => SetProperty(ref _moq, value);
        }

        [NotMapped]
        public decimal RemainingQty => OrderQty - ReceivedQty < 0
            ? 0m
            : OrderQty - ReceivedQty;

        [NotMapped] public bool IsFullyReceived => OrderQty > 0 && ReceivedQty >= OrderQty;

        [NotMapped]
        public string FullDisplayName =>
            BuildDisplayName(_description, _variantDescription, fallback: _itemCode);

        [NotMapped]
        public string ReceiptDisplayName =>
            BuildDisplayName(
                string.IsNullOrWhiteSpace(_printName) ? _description : _printName,
                _variantDescription,
                fallback: FullDisplayName);

        [NotMapped]
        public string DisplayName => FullDisplayName;

        [NotMapped]
        public string VariantDisplayName => IsStandardVariant(_variantDescription) ? "Standard" : NormalizeText(_variantDescription);

        [NotMapped]
        public decimal GrossAmount => Math.Round(OrderQty * ExpectedCost, 2);

        private decimal _globalDiscountAllocation;
        [NotMapped]
        public decimal GlobalDiscountAllocation
        {
            get => _globalDiscountAllocation;
            set => SetProperty(ref _globalDiscountAllocation, value);
        }

        private decimal _taxableAmountPreview;
        [NotMapped]
        public decimal TaxableAmountPreview
        {
            get => _taxableAmountPreview;
            set => SetProperty(ref _taxableAmountPreview, value);
        }

        [NotMapped]
        public string TaxCategoryDisplay =>
            string.IsNullOrWhiteSpace(_taxCategoryCodeSnapshot)
                ? "Unclassified"
                : string.IsNullOrWhiteSpace(_taxNameSnapshot)
                    ? _taxCategoryCodeSnapshot
                    : $"{_taxCategoryCodeSnapshot} - {_taxNameSnapshot}";

        [NotMapped]
        public decimal VatAmount
        {
            get => _taxAmount;
            set => SetProperty(ref _taxAmount, value);
        }

        // =========================================================
        // SAVED LINE DATA
        // =========================================================

        [MaxLength(20)] [ObservableProperty] private string _uom = string.Empty;

        // Kept for old database compatibility.
        // New Item Master UI does not need to show this as "Vendor SKU".
        [MaxLength(100)] [ObservableProperty] private string _supplierItemCode = string.Empty;

        [Column(TypeName = "decimal(18,3)")] [ObservableProperty] private decimal _orderQty = 0m;
        partial void OnOrderQtyChanged(decimal value) => RecalculateLineAmounts();

        // Updated by GRN receiving, not by PO editing.
        [Column(TypeName = "decimal(18,3)")] [ObservableProperty] private decimal _receivedQty = 0m;

        [Column(TypeName = "decimal(18,2)")] [ObservableProperty] private decimal _expectedCost = 0m;
        partial void OnExpectedCostChanged(decimal value) => RecalculateLineAmounts();

        // Amount / Percent.
        [MaxLength(20)] [ObservableProperty] private string _lineDiscountMode = "Amount";
        partial void OnLineDiscountModeChanged(string value) => RecalculateLineAmounts();

        // User-entered value.
        // Example:
        // Mode Amount  -> 500
        // Mode Percent -> 10
        [Column(TypeName = "decimal(18,2)")] [ObservableProperty] private decimal _lineDiscountValue = 0m;
        partial void OnLineDiscountValueChanged(decimal value) => RecalculateLineAmounts();

        // Final calculated discount amount.
        // Keep this because old code already uses LineDiscount.
        [Column(TypeName = "decimal(18,2)")] [ObservableProperty] private decimal _lineDiscount = 0m;

        // Product VAT only. No income tax/accounting tax.
        [MaxLength(20)] [ObservableProperty] private string _taxCode = "VAT";

        [Column(TypeName = "decimal(5,2)")] [ObservableProperty] private decimal _vatRatePercent = 0m;
        partial void OnVatRatePercentChanged(decimal value) => RecalculateLineAmounts();

        // False = VAT added on top.
        // True = ExpectedCost already includes VAT.
        [ObservableProperty] private bool _isVatIncluded = false;
        partial void OnIsVatIncludedChanged(bool value) => RecalculateLineAmounts();

        // Existing field name kept for compatibility.
        // This is product VAT amount.
        [Column(TypeName = "decimal(18,2)")] [ObservableProperty] private decimal _taxAmount = 0m;

        [Column(TypeName = "decimal(18,2)")] [ObservableProperty] private decimal _lineTotal = 0m;

        // =========================================================
        // IMMUTABLE TAX SNAPSHOT FOUNDATION
        // =========================================================
        // These fields are deliberately separate from the legacy tax
        // fields above. Existing and interim documents remain marked
        // LegacyUnknown until the shared VAT engine writes a complete set.

        [ObservableProperty]
        private int? _taxCategoryId;

        public TaxCategory? TaxCategory { get; set; }

        public int? TaxRateId { get; set; }

        public TaxRate? TaxRate { get; set; }

        [MaxLength(30)] [ObservableProperty] private string? _taxCategoryCodeSnapshot;
        [MaxLength(20)] [ObservableProperty] private string? _taxCodeSnapshot;
        [MaxLength(100)] [ObservableProperty] private string? _taxNameSnapshot;
        [Column(TypeName = "decimal(7,4)")] [ObservableProperty] private decimal? _taxRatePercentSnapshot;

        public bool? IsTaxInclusiveSnapshot { get; set; } // This is not directly bound or changed in the UI

        [Column(TypeName = "decimal(18,2)")] [ObservableProperty] private decimal? _taxableAmountSnapshot;
        [Column(TypeName = "decimal(18,2)")] [ObservableProperty] private decimal? _vatAmountSnapshot;
        [Column(TypeName = "decimal(18,2)")] [ObservableProperty] private decimal? _taxInclusiveAmountSnapshot;

        [Required]
        [MaxLength(30)]
        public string TaxSnapshotStatus { get; set; } = TaxSnapshotStatuses.LegacyUnknown;

        // Open, Closed, Cancelled.
        // We will remove Partially Received behavior from the user workflow.
        [MaxLength(30)] [ObservableProperty] private string _lineStatus = "Open";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public DateTime? ClosedAt { get; set; }

        public void RecalculateLineAmounts()
        {
            LineDiscountMode = NormalizeDiscountMode(LineDiscountMode);

            if (LineDiscountValue <= 0 && LineDiscount > 0 && LineDiscountMode == "Amount")
            {
                LineDiscountValue = LineDiscount;
            }

            decimal gross = OrderQty * ExpectedCost;

            LineDiscount = CalculateDiscountAmount(gross, LineDiscountMode, LineDiscountValue);

            decimal afterDiscount = gross - LineDiscount;

            if (afterDiscount < 0)
                afterDiscount = 0m;

            decimal vatRate = VatRatePercent / 100m;

            if (VatRatePercent <= 0)
            {
                TaxAmount = 0m;
                LineTotal = Math.Round(afterDiscount, 2);
            }
            else if (IsVatIncluded)
            {
                TaxAmount = Math.Round(afterDiscount - (afterDiscount / (1 + vatRate)), 2);
                LineTotal = Math.Round(afterDiscount, 2);
            }
            else
            {
                TaxAmount = Math.Round(afterDiscount * vatRate, 2);
                LineTotal = Math.Round(afterDiscount + TaxAmount, 2);
            }
        }

        private static decimal CalculateDiscountAmount(decimal gross, string? discountMode, decimal discountValue)
        {
            if (gross <= 0 || discountValue <= 0)
                return 0m;

            if (IsPercentDiscount(discountMode))
                return Math.Round(gross * discountValue / 100m, 2);

            return Math.Round(discountValue, 2);
        }

        private static bool IsPercentDiscount(string? value) => NormalizeDiscountMode(value) == "Percent";

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

        private static string NormalizeDiscountMode(string? value)
        {
            string mode = (value ?? string.Empty).Trim();

            if (mode.Equals("Percent", StringComparison.OrdinalIgnoreCase) || mode.Equals("%", StringComparison.OrdinalIgnoreCase))
            {
                return "Percent";
            }

            return "Amount";
        }
    }
}