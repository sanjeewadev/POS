using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using POS.Core.Configuration;
using POS.Core.Utilities;

namespace POS.Core.Models.DTOs
{
    public class GrnPoLookupDto
    {
        public int PoHeaderId { get; set; }

        public string PoNumber { get; set; } = string.Empty;

        public int SupplierId { get; set; }

        public string SupplierName { get; set; } = string.Empty;

        public DateTime OrderDate { get; set; }

        public DateTime ExpectedDate { get; set; }

        public decimal NetPayable { get; set; }

        public string Status { get; set; } = string.Empty;

        public decimal TotalOrderedQty { get; set; }

        public decimal TotalReceivedQty { get; set; }

        public decimal TotalOutstandingQty => TotalOrderedQty - TotalReceivedQty < 0
            ? 0m
            : TotalOrderedQty - TotalReceivedQty;

        public string DisplayText =>
            $"{PoNumber} | {SupplierName} | Ordered: {QuantityDisplayFormatter.Format(TotalOrderedQty)} | Received: {QuantityDisplayFormatter.Format(TotalReceivedQty)}";
    }

    public class GrnPoLineDto
    {
        public int PoLineId { get; set; }

        public int ItemVariantId { get; set; }

        public string ItemCode { get; set; } = string.Empty;

        public string SkuCode { get; set; } = string.Empty;

        public string Barcode { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string PrintName { get; set; } = string.Empty;

        public string VariantDescription { get; set; } = string.Empty;

        public string Uom { get; set; } = string.Empty;

        public decimal OrderedQty { get; set; }

        public decimal AlreadyReceivedQty { get; set; }

        public decimal OutstandingQty => OrderedQty - AlreadyReceivedQty < 0
            ? 0m
            : OrderedQty - AlreadyReceivedQty;

        public decimal ExpectedCost { get; set; }

        public bool HasBatchTracking { get; set; } = true;

        public bool HasExpiryTracking { get; set; } = false;

        public bool IsScaleItem { get; set; } = false;

        public bool AllowDecimalQuantity { get; set; } = false;

        public bool RequiresExpiry
        {
            get => HasExpiryTracking;
            set => HasExpiryTracking = value;
        }

        public string LineDiscountMode { get; set; } = "Amount";

        public decimal LineDiscountValue { get; set; } = 0m;

        public decimal LineDiscount { get; set; } = 0m;

        public decimal VatRatePercent { get; set; } = 0m;

        public bool IsVatIncluded { get; set; } = false;

        public decimal VatAmount { get; set; } = 0m;

        public string TaxCategoryCode { get; set; } = string.Empty;

        public string TaxCategoryName { get; set; } = string.Empty;

        public decimal CurrentRetailPrice { get; set; } = 0m;

        public decimal CurrentWholesalePrice { get; set; } = 0m;

        public decimal CurrentMinimumPrice { get; set; } = 0m;

        public decimal CurrentMaximumPrice { get; set; } = 0m;

        public string FullDisplayName =>
            GrnDisplayNameHelper.BuildDisplayName(Description, VariantDescription, SkuCode);

        public string ReceiptDisplayName =>
            GrnDisplayNameHelper.BuildDisplayName(
                string.IsNullOrWhiteSpace(PrintName) ? Description : PrintName,
                VariantDescription,
                FullDisplayName);

        public string DisplayName => FullDisplayName;

        public string VariantDisplayName =>
            GrnDisplayNameHelper.IsStandardVariantDescription(VariantDescription)
                ? "Standard"
                : GrnDisplayNameHelper.NormalizeText(VariantDescription);
    }

    public class GrnVariantLookupDto
    {
        public int ItemVariantId { get; set; }

        public int ItemParentId { get; set; }

        public string ItemCode { get; set; } = string.Empty;

        public string SkuCode { get; set; } = string.Empty;

        public string Barcode { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string PrintName { get; set; } = string.Empty;

        public string VariantDescription { get; set; } = string.Empty;

        public string Uom { get; set; } = string.Empty;

        public decimal LastSupplierCost { get; set; }

        public decimal CurrentCost { get; set; }

        public bool HasBatchTracking { get; set; } = true;

        public bool HasExpiryTracking { get; set; } = false;

        public bool IsScaleItem { get; set; } = false;

        public bool AllowDecimalQuantity { get; set; } = false;

        public bool RequiresExpiry
        {
            get => HasExpiryTracking;
            set => HasExpiryTracking = value;
        }

        public string LineDiscountMode { get; set; } = "Amount";

        public decimal LineDiscountValue { get; set; } = 0m;

        public decimal LineDiscount { get; set; } = 0m;

        public decimal VatRatePercent { get; set; } = 0m;

        public bool IsVatIncluded { get; set; } = false;

        public decimal VatAmount { get; set; } = 0m;

        public string TaxCategoryCode { get; set; } = string.Empty;

        public string TaxCategoryName { get; set; } = string.Empty;

        public decimal CurrentRetailPrice { get; set; } = 0m;

        public decimal CurrentWholesalePrice { get; set; } = 0m;

        public decimal CurrentMinimumPrice { get; set; } = 0m;

        public decimal CurrentMaximumPrice { get; set; } = 0m;

        public string FullDisplayName =>
            GrnDisplayNameHelper.BuildDisplayName(Description, VariantDescription, SkuCode);

        public string ReceiptDisplayName =>
            GrnDisplayNameHelper.BuildDisplayName(
                string.IsNullOrWhiteSpace(PrintName) ? Description : PrintName,
                VariantDescription,
                FullDisplayName);

        public string DisplayName => FullDisplayName;

        public string VariantDisplayName =>
            GrnDisplayNameHelper.IsStandardVariantDescription(VariantDescription)
                ? "Standard"
                : GrnDisplayNameHelper.NormalizeText(VariantDescription);
    }

    public sealed class GrnBatchPriceContextDto
    {
        public decimal RetailPrice { get; set; }
        public decimal WholesalePrice { get; set; }
        public decimal MinimumPrice { get; set; }
        public decimal MaximumPrice { get; set; }
        public string PriceSource { get; set; } = "Master Price";
    }

    public sealed class GrnTaxPreviewLineDto
    {
        public int SourceIndex { get; set; }

        public int ItemVariantId { get; set; }

        public string TaxCategoryCode { get; set; } = string.Empty;

        public string TaxCategoryName { get; set; } = string.Empty;

        public string TaxCode { get; set; } = string.Empty;

        public decimal VatRatePercent { get; set; }

        public decimal GrossAmount { get; set; }

        public decimal LineDiscountAmount { get; set; }

        public decimal GlobalDiscountAllocation { get; set; }

        public decimal TaxableAmount { get; set; }

        public decimal VatAmount { get; set; }

        public decimal TaxInclusiveAmount { get; set; }

        public decimal LandedCost { get; set; }
    }

    public sealed class GrnTaxPreviewDto
    {
        public List<GrnTaxPreviewLineDto> Lines { get; set; } = new();

        public decimal Subtotal { get; set; }

        public decimal LineDiscountTotal { get; set; }

        public decimal GlobalDiscount { get; set; }

        public decimal TotalDiscount { get; set; }

        public decimal TotalVat { get; set; }

        public decimal FreightAmount { get; set; }

        public decimal NetPayable { get; set; }

        public decimal TaxableAmountTotal { get; set; }

        public decimal StandardRatedAmount { get; set; }

        public decimal ZeroRatedAmount { get; set; }

        public decimal ExemptAmount { get; set; }

        public decimal OutOfScopeAmount { get; set; }
    }

    public partial class GrnLineEntryDto : ObservableObject
    {
        private bool _isRecalculating;

        public int GrnLineId { get; set; }

        public int? PoLineId { get; set; }

        public int? ItemBatchId { get; set; }

        public int ItemVariantId { get; set; }

        public string ItemCode { get; set; } = string.Empty;

        public string SkuCode { get; set; } = string.Empty;

        public string Barcode { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string PrintName { get; set; } = string.Empty;

        public string VariantDescription { get; set; } = string.Empty;

        public string Uom { get; set; } = string.Empty;

        public decimal OrderedQty { get; set; }

        public decimal OutstandingPoQty { get; set; }

        public bool HasBatchTracking { get; set; } = true;

        public bool HasExpiryTracking { get; set; } = false;

        public bool IsScaleItem { get; set; } = false;

        public bool AllowDecimalQuantity { get; set; } = false;

        // Internal only. This comes from Item Master.
        // Do not show this as a user-editable checkbox.
        [ObservableProperty]
        private bool _requiresExpiry;

        [ObservableProperty]
        private string _batchNo = string.Empty;

        [ObservableProperty]
        private DateTime? _expiryDate;

        [ObservableProperty]
        private decimal _receivedQty;

        [ObservableProperty]
        private decimal _unitCost;

        // Amount / Percent.
        [ObservableProperty]
        private string _lineDiscountMode = "Amount";

        // User-entered discount value.
        // Amount mode  -> Rs amount.
        // Percent mode -> percent.
        [ObservableProperty]
        private decimal _lineDiscountValue;

        // Final calculated discount amount.
        // Kept for compatibility with existing GRN logic.
        [ObservableProperty]
        private decimal _lineDiscount;

        [ObservableProperty]
        private decimal _vatRatePercent;

        [ObservableProperty]
        private bool _isVatIncluded;

        [ObservableProperty]
        private decimal _vatAmount;

        [ObservableProperty]
        private decimal _landedCost;

        [ObservableProperty]
        private decimal _lineTotal;

        [ObservableProperty]
        private string _taxCategoryCode = string.Empty;

        [ObservableProperty]
        private string _taxCategoryName = string.Empty;

        [ObservableProperty]
        private decimal _taxableAmount;

        [ObservableProperty]
        private decimal _globalDiscountAllocation;

        // =========================================================
        // SELLING PRICE UPDATE FIELDS
        // =========================================================

        [ObservableProperty]
        private string _sellingPriceAction = GrnSellingPriceActionCodes.UseCurrentMasterPrice;

        [ObservableProperty]
        private string _currentPriceSource = "Master Price";

        [ObservableProperty]
        private bool _updateSellingPrices;

        [ObservableProperty]
        private decimal _currentRetailPrice;

        [ObservableProperty]
        private decimal _newRetailPrice;

        [ObservableProperty]
        private decimal _currentWholesalePrice;

        [ObservableProperty]
        private decimal _newWholesalePrice;

        [ObservableProperty]
        private decimal _currentMinimumPrice;

        [ObservableProperty]
        private decimal _newMinimumPrice;

        [ObservableProperty]
        private decimal _currentMaximumPrice;

        [ObservableProperty]
        private decimal _newMaximumPrice;

        [ObservableProperty]
        private decimal _retailMarkupPercent;

        [ObservableProperty]
        private decimal _wholesaleMarkupPercent;

        // =========================================================
        // DISPLAY HELPERS
        // =========================================================

        public bool IsExpiryEnabled => RequiresExpiry;

        public string ExpiryRequirementText => RequiresExpiry
            ? "Expiry Required"
            : "No Expiry";

        public string FullDisplayName =>
            GrnDisplayNameHelper.BuildDisplayName(Description, VariantDescription, SkuCode);

        public string ReceiptDisplayName =>
            GrnDisplayNameHelper.BuildDisplayName(
                string.IsNullOrWhiteSpace(PrintName) ? Description : PrintName,
                VariantDescription,
                FullDisplayName);

        public string DisplayName => FullDisplayName;

        public string VariantDisplayName =>
            GrnDisplayNameHelper.IsStandardVariantDescription(VariantDescription)
                ? "Standard"
                : GrnDisplayNameHelper.NormalizeText(VariantDescription);

        public string BatchDisplayText
        {
            get
            {
                if (string.IsNullOrWhiteSpace(BatchNo))
                    return "[AUTO]";

                return BatchNo.Trim();
            }
        }

        public string ExpiryDisplayText
        {
            get
            {
                if (!ExpiryDate.HasValue)
                    return RequiresExpiry ? "Required" : "No Expiry";

                return ExpiryDate.Value.ToString("yyyy-MM-dd");
            }
        }

        public string TrackingText
        {
            get
            {
                if (!HasBatchTracking)
                    return "No Batch";

                return RequiresExpiry ? "Batch + Expiry" : "Batch";
            }
        }

        public string VatDisplayText
        {
            get
            {
                string category = string.IsNullOrWhiteSpace(TaxCategoryName)
                    ? TaxCategoryCode
                    : TaxCategoryName;

                if (VatRatePercent <= 0)
                    return string.IsNullOrWhiteSpace(category) ? "No VAT" : category;

                string mode = IsVatIncluded ? "Included" : "Added";
                return string.IsNullOrWhiteSpace(category)
                    ? $"VAT {VatRatePercent:N2}% {mode}"
                    : $"{category} {VatRatePercent:N2}% {mode}";
            }
        }

        public string TaxCategoryDisplayText
        {
            get
            {
                string name = string.IsNullOrWhiteSpace(TaxCategoryName)
                    ? TaxCategoryCode
                    : TaxCategoryName;

                if (string.IsNullOrWhiteSpace(name))
                    name = "Tax pending";

                return VatRatePercent > 0m
                    ? $"{name} ({VatRatePercent:N2}%)"
                    : name;
            }
        }

        public bool IsUseCurrentMasterPriceAction =>
            GrnSellingPriceActionCodes.Normalize(SellingPriceAction, UpdateSellingPrices) ==
            GrnSellingPriceActionCodes.UseCurrentMasterPrice;

        public bool IsUpdateMasterPriceAction =>
            GrnSellingPriceActionCodes.Normalize(SellingPriceAction, UpdateSellingPrices) ==
            GrnSellingPriceActionCodes.UpdateMasterPrice;

        public bool IsBatchPriceOverrideAction =>
            GrnSellingPriceActionCodes.Normalize(SellingPriceAction, UpdateSellingPrices) ==
            GrnSellingPriceActionCodes.SetBatchPriceOverride;

        public string PriceActionText =>
            GrnSellingPriceActionCodes.ToDisplayText(SellingPriceAction);

        public bool HasRetailPriceChange =>
            (IsUpdateMasterPriceAction || IsBatchPriceOverrideAction) &&
            Math.Round(CurrentRetailPrice, 2) != Math.Round(NewRetailPrice, 2);

        public bool HasWholesalePriceChange =>
            (IsUpdateMasterPriceAction || IsBatchPriceOverrideAction) &&
            Math.Round(CurrentWholesalePrice, 2) != Math.Round(NewWholesalePrice, 2);

        public bool HasMinimumPriceChange =>
            IsUpdateMasterPriceAction &&
            Math.Round(CurrentMinimumPrice, 2) != Math.Round(NewMinimumPrice, 2);

        public bool HasMaximumPriceChange =>
            IsUpdateMasterPriceAction &&
            Math.Round(CurrentMaximumPrice, 2) != Math.Round(NewMaximumPrice, 2);

        public bool HasAnySellingPriceChange =>
            !IsUseCurrentMasterPriceAction;

        public string PriceUpdateText
        {
            get
            {
                if (IsUseCurrentMasterPriceAction)
                    return "Use Current Master Price";

                if (IsBatchPriceOverrideAction)
                {
                    return $"Batch Override | Retail {NewRetailPrice:N2} | W/S {NewWholesalePrice:N2}";
                }

                var parts = new List<string>();
                if (HasRetailPriceChange) parts.Add($"Retail {CurrentRetailPrice:N2} → {NewRetailPrice:N2}");
                if (HasWholesalePriceChange) parts.Add($"W/S {CurrentWholesalePrice:N2} → {NewWholesalePrice:N2}");
                if (HasMinimumPriceChange) parts.Add($"Min {CurrentMinimumPrice:N2} → {NewMinimumPrice:N2}");
                if (HasMaximumPriceChange) parts.Add($"Max {CurrentMaximumPrice:N2} → {NewMaximumPrice:N2}");
                return parts.Count == 0 ? "Update Master Price (no amount change)" : string.Join(" | ", parts);
            }
        }

        public decimal GrossAmount => Math.Round(ReceivedQty * UnitCost, 2);

        public decimal NetBeforeVat
        {
            get
            {
                decimal net = GrossAmount - LineDiscount;
                return net < 0 ? 0m : Math.Round(net, 2);
            }
        }

        public List<string> ValidateForPost(bool isPoLinked)
        {
            RecalculateLineAmounts();

            var errors = new List<string>();

            if (ItemVariantId <= 0)
                errors.Add("Invalid item variant.");

            if (ReceivedQty <= 0)
                errors.Add($"{DisplayName}: received quantity must be greater than zero.");

            if (!AllowDecimalQuantity && HasDecimalPart(ReceivedQty))
            {
                errors.Add(
                    $"{DisplayName}: decimal quantity is not allowed for UOM '{Uom}'.");
            }

            if (UnitCost <= 0)
                errors.Add($"{DisplayName}: unit cost must be greater than zero.");

            if (!IsValidDiscountMode(LineDiscountMode))
                errors.Add($"{DisplayName}: discount mode must be Amount or Percent.");

            if (LineDiscountValue < 0)
                errors.Add($"{DisplayName}: discount value cannot be negative.");

            if (IsPercentDiscount && LineDiscountValue > 100)
                errors.Add($"{DisplayName}: discount percentage cannot be greater than 100.");

            if (LineDiscount < 0)
                errors.Add($"{DisplayName}: discount amount cannot be negative.");

            if (LineDiscount > GrossAmount)
                errors.Add($"{DisplayName}: discount amount cannot be greater than line value.");

            if (VatRatePercent < 0 || VatRatePercent > 100)
                errors.Add($"{DisplayName}: VAT rate must be between 0 and 100.");

            if (BatchNo != null && BatchNo.Trim().Length > 50)
                errors.Add($"{DisplayName}: batch number cannot be longer than 50 characters.");

            if (RequiresExpiry && !ExpiryDate.HasValue)
                errors.Add($"{DisplayName}: expiry date is required.");

            if (isPoLinked && OutstandingPoQty > 0 && ReceivedQty > OutstandingPoQty)
                errors.Add($"{DisplayName}: received quantity cannot exceed PO ordered quantity.");

            string priceAction = GrnSellingPriceActionCodes.Normalize(SellingPriceAction, UpdateSellingPrices);
            if (!GrnSellingPriceActionCodes.IsValid(priceAction))
                errors.Add($"{DisplayName}: invalid selling price action.");

            if (priceAction == GrnSellingPriceActionCodes.SetBatchPriceOverride && !HasBatchTracking)
                errors.Add($"{DisplayName}: a batch-only price override requires a batch-tracked Stock Item.");

            if (priceAction == GrnSellingPriceActionCodes.UpdateMasterPrice ||
                priceAction == GrnSellingPriceActionCodes.SetBatchPriceOverride)
            {
                if (NewRetailPrice <= 0m || NewWholesalePrice <= 0m)
                    errors.Add($"{DisplayName}: Retail and Wholesale prices must be greater than zero.");

                if (NewRetailPrice < 0m || NewWholesalePrice < 0m ||
                    NewMinimumPrice < 0m || NewMaximumPrice < 0m)
                    errors.Add($"{DisplayName}: selling prices cannot be negative.");

                decimal minimum = priceAction == GrnSellingPriceActionCodes.UpdateMasterPrice
                    ? NewMinimumPrice
                    : CurrentMinimumPrice;
                decimal maximum = priceAction == GrnSellingPriceActionCodes.UpdateMasterPrice
                    ? NewMaximumPrice
                    : CurrentMaximumPrice;

                if (maximum > 0m && minimum > maximum)
                    errors.Add($"{DisplayName}: minimum price cannot be greater than maximum price.");
                if (minimum > 0m && (NewRetailPrice < minimum || NewWholesalePrice < minimum))
                    errors.Add($"{DisplayName}: Retail and Wholesale prices cannot be below the master Minimum price.");
                if (maximum > 0m && (NewRetailPrice > maximum || NewWholesalePrice > maximum))
                    errors.Add($"{DisplayName}: Retail and Wholesale prices cannot be above the master Maximum price.");

                if (RetailMarkupPercent < -100 || WholesaleMarkupPercent < -100)
                    errors.Add($"{DisplayName}: markup percentage is invalid.");
            }

            return errors;
        }

        public void RecalculateLineAmounts()
        {
            if (_isRecalculating)
                return;

            _isRecalculating = true;

            try
            {
                decimal gross = GrossAmount;

                LineDiscount = CalculateDiscountAmount(
                    gross,
                    LineDiscountMode,
                    LineDiscountValue);

                decimal netBeforeVat = gross - LineDiscount;

                if (netBeforeVat < 0)
                    netBeforeVat = 0m;

                decimal vatRate = VatRatePercent / 100m;

                if (VatRatePercent <= 0)
                {
                    VatAmount = 0m;
                    LineTotal = Math.Round(netBeforeVat, 2);
                    return;
                }

                if (IsVatIncluded)
                {
                    VatAmount = Math.Round(
                        netBeforeVat - (netBeforeVat / (1 + vatRate)),
                        2);

                    LineTotal = Math.Round(netBeforeVat, 2);
                }
                else
                {
                    VatAmount = Math.Round(netBeforeVat * vatRate, 2);
                    LineTotal = Math.Round(netBeforeVat + VatAmount, 2);
                }
            }
            finally
            {
                _isRecalculating = false;
            }

            OnPropertyChanged(nameof(GrossAmount));
            OnPropertyChanged(nameof(NetBeforeVat));
            OnPropertyChanged(nameof(VatDisplayText));
        }

        public void ApplyRetailMarkupFromLandedCost()
        {
            if (LandedCost <= 0)
                return;

            if (RetailMarkupPercent != 0)
            {
                NewRetailPrice = Math.Round(
                    LandedCost + (LandedCost * RetailMarkupPercent / 100m),
                    2);
            }

            if (WholesaleMarkupPercent != 0)
            {
                NewWholesalePrice = Math.Round(
                    LandedCost + (LandedCost * WholesaleMarkupPercent / 100m),
                    2);
            }
        }

        partial void OnSellingPriceActionChanged(string value)
        {
            string normalized = GrnSellingPriceActionCodes.Normalize(value);
            if (!string.Equals(value, normalized, StringComparison.Ordinal))
            {
                SellingPriceAction = normalized;
                return;
            }

            UpdateSellingPrices =
                normalized == GrnSellingPriceActionCodes.UpdateMasterPrice;

            if (normalized == GrnSellingPriceActionCodes.UseCurrentMasterPrice)
            {
                NewRetailPrice = CurrentRetailPrice;
                NewWholesalePrice = CurrentWholesalePrice;
                NewMinimumPrice = CurrentMinimumPrice;
                NewMaximumPrice = CurrentMaximumPrice;
            }

            OnPropertyChanged(nameof(IsUseCurrentMasterPriceAction));
            OnPropertyChanged(nameof(IsUpdateMasterPriceAction));
            OnPropertyChanged(nameof(IsBatchPriceOverrideAction));
            OnPropertyChanged(nameof(PriceActionText));
            OnPropertyChanged(nameof(HasAnySellingPriceChange));
            OnPropertyChanged(nameof(PriceUpdateText));
        }

        partial void OnBatchNoChanged(string value)
        {
            OnPropertyChanged(nameof(BatchDisplayText));
        }

        partial void OnExpiryDateChanged(DateTime? value)
        {
            OnPropertyChanged(nameof(ExpiryDisplayText));
        }

        partial void OnRequiresExpiryChanged(bool value)
        {
            if (!value)
                ExpiryDate = null;

            HasExpiryTracking = value;

            OnPropertyChanged(nameof(IsExpiryEnabled));
            OnPropertyChanged(nameof(ExpiryRequirementText));
            OnPropertyChanged(nameof(ExpiryDisplayText));
            OnPropertyChanged(nameof(TrackingText));
        }

        partial void OnReceivedQtyChanged(decimal value)
        {
            RecalculateLineAmounts();
        }

        partial void OnUnitCostChanged(decimal value)
        {
            RecalculateLineAmounts();
        }

        partial void OnLineDiscountModeChanged(string value)
        {
            RecalculateLineAmounts();
        }

        partial void OnLineDiscountValueChanged(decimal value)
        {
            RecalculateLineAmounts();
        }

        partial void OnLineDiscountChanged(decimal value)
        {
            if (_isRecalculating)
                return;

            if (IsAmountDiscount)
                LineDiscountValue = value;

            OnPropertyChanged(nameof(NetBeforeVat));
        }

        partial void OnVatRatePercentChanged(decimal value)
        {
            RecalculateLineAmounts();
            OnPropertyChanged(nameof(TaxCategoryDisplayText));
            OnPropertyChanged(nameof(VatDisplayText));
        }

        partial void OnIsVatIncludedChanged(bool value)
        {
            RecalculateLineAmounts();
        }

        partial void OnLandedCostChanged(decimal value)
        {
            OnPropertyChanged(nameof(PriceUpdateText));
        }

        partial void OnRetailMarkupPercentChanged(decimal value)
        {
            OnPropertyChanged(nameof(PriceUpdateText));
        }

        partial void OnWholesaleMarkupPercentChanged(decimal value)
        {
            OnPropertyChanged(nameof(PriceUpdateText));
        }

        partial void OnUpdateSellingPricesChanged(bool value)
        {
            if (value &&
                SellingPriceAction == GrnSellingPriceActionCodes.UseCurrentMasterPrice)
            {
                SellingPriceAction = GrnSellingPriceActionCodes.UpdateMasterPrice;
                return;
            }

            if (value)
            {
                if (NewRetailPrice <= 0)
                    NewRetailPrice = CurrentRetailPrice;

                if (NewWholesalePrice <= 0)
                    NewWholesalePrice = CurrentWholesalePrice;

                if (NewMinimumPrice <= 0)
                    NewMinimumPrice = CurrentMinimumPrice;

                if (NewMaximumPrice <= 0)
                    NewMaximumPrice = CurrentMaximumPrice;
            }

            OnPropertyChanged(nameof(HasRetailPriceChange));
            OnPropertyChanged(nameof(HasWholesalePriceChange));
            OnPropertyChanged(nameof(HasMinimumPriceChange));
            OnPropertyChanged(nameof(HasMaximumPriceChange));
            OnPropertyChanged(nameof(HasAnySellingPriceChange));
            OnPropertyChanged(nameof(PriceUpdateText));
        }

        partial void OnTaxCategoryCodeChanged(string value)
        {
            OnPropertyChanged(nameof(TaxCategoryDisplayText));
            OnPropertyChanged(nameof(VatDisplayText));
        }

        partial void OnTaxCategoryNameChanged(string value)
        {
            OnPropertyChanged(nameof(TaxCategoryDisplayText));
            OnPropertyChanged(nameof(VatDisplayText));
        }

        partial void OnNewRetailPriceChanged(decimal value)
        {
            OnPropertyChanged(nameof(HasRetailPriceChange));
            OnPropertyChanged(nameof(HasAnySellingPriceChange));
            OnPropertyChanged(nameof(PriceUpdateText));
        }

        partial void OnNewWholesalePriceChanged(decimal value)
        {
            OnPropertyChanged(nameof(HasWholesalePriceChange));
            OnPropertyChanged(nameof(HasAnySellingPriceChange));
            OnPropertyChanged(nameof(PriceUpdateText));
        }

        partial void OnCurrentRetailPriceChanged(decimal value)
        {
            OnPropertyChanged(nameof(HasRetailPriceChange));
            OnPropertyChanged(nameof(HasAnySellingPriceChange));
            OnPropertyChanged(nameof(PriceUpdateText));
        }

        partial void OnCurrentWholesalePriceChanged(decimal value)
        {
            OnPropertyChanged(nameof(HasWholesalePriceChange));
            OnPropertyChanged(nameof(HasAnySellingPriceChange));
            OnPropertyChanged(nameof(PriceUpdateText));
        }

        partial void OnNewMinimumPriceChanged(decimal value)
        {
            OnPropertyChanged(nameof(HasMinimumPriceChange));
            OnPropertyChanged(nameof(HasAnySellingPriceChange));
            OnPropertyChanged(nameof(PriceUpdateText));
        }

        partial void OnNewMaximumPriceChanged(decimal value)
        {
            OnPropertyChanged(nameof(HasMaximumPriceChange));
            OnPropertyChanged(nameof(HasAnySellingPriceChange));
            OnPropertyChanged(nameof(PriceUpdateText));
        }

        partial void OnCurrentMinimumPriceChanged(decimal value)
        {
            OnPropertyChanged(nameof(HasMinimumPriceChange));
            OnPropertyChanged(nameof(HasAnySellingPriceChange));
            OnPropertyChanged(nameof(PriceUpdateText));
        }

        partial void OnCurrentMaximumPriceChanged(decimal value)
        {
            OnPropertyChanged(nameof(HasMaximumPriceChange));
            OnPropertyChanged(nameof(HasAnySellingPriceChange));
            OnPropertyChanged(nameof(PriceUpdateText));
        }

        private bool IsAmountDiscount =>
            NormalizeDiscountMode(LineDiscountMode) == "Amount";

        private bool IsPercentDiscount =>
            NormalizeDiscountMode(LineDiscountMode) == "Percent";

        private static decimal CalculateDiscountAmount(
            decimal gross,
            string? discountMode,
            decimal discountValue)
        {
            if (gross <= 0 || discountValue <= 0)
                return 0m;

            string mode = NormalizeDiscountMode(discountMode);

            if (mode == "Percent")
                return Math.Round(gross * discountValue / 100m, 2);

            return Math.Round(discountValue, 2);
        }

        private static bool IsValidDiscountMode(string? value)
        {
            string mode = NormalizeDiscountMode(value);

            return mode == "Amount" || mode == "Percent";
        }

        private static string NormalizeDiscountMode(string? value)
        {
            string mode = (value ?? string.Empty).Trim();

            if (mode.Equals("Percent", StringComparison.OrdinalIgnoreCase) ||
                mode.Equals("%", StringComparison.OrdinalIgnoreCase))
            {
                return "Percent";
            }

            return "Amount";
        }

        private static bool HasDecimalPart(decimal value)
        {
            return value != Math.Truncate(value);
        }
    }

    internal static class GrnDisplayNameHelper
    {
        public static string BuildDisplayName(
            string? baseName,
            string? variantDescription,
            string? fallback)
        {
            string cleanBaseName = NormalizeText(baseName);
            string cleanVariant = NormalizeText(variantDescription);
            string cleanFallback = NormalizeText(fallback);

            if (IsStandardVariantDescription(cleanVariant))
            {
                if (!string.IsNullOrWhiteSpace(cleanBaseName))
                    return cleanBaseName;

                return cleanFallback;
            }

            if (string.IsNullOrWhiteSpace(cleanBaseName))
                return cleanVariant;

            return $"{cleanBaseName} - {cleanVariant}";
        }

        public static bool IsStandardVariantDescription(string? value)
        {
            string cleanValue = NormalizeText(value);

            return string.IsNullOrWhiteSpace(cleanValue) ||
                   cleanValue.Equals("Standard", StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}