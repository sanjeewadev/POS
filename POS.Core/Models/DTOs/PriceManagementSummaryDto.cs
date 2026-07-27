using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using POS.Core.Configuration;
using POS.Core.Services.Pricing;

namespace POS.Core.Models.DTOs
{
    public class PriceManagementSummaryDto : INotifyPropertyChanged
    {
        private bool _suppressDirtyTracking;
        private decimal _retailPrice;
        private decimal _wholesalePrice;
        private decimal _minimumPrice;
        private decimal _maximumPrice;
        private bool _isDirty;

        public int ItemVariantId { get; set; }
        public int ItemParentId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string SkuCode { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string VariantAttributes { get; set; } = string.Empty;
        public string Uom { get; set; } = "PCS";
        public string CategoryName { get; set; } = string.Empty;
        public string ItemType { get; set; } = ItemTypeCodes.StockItem;
        public bool HasBatchTracking { get; set; }
        public bool HasExpiryTracking { get; set; }
        public int ActiveStockRowCount { get; set; }
        public int PhysicalBatchCount { get; set; }
        public int BatchOverrideCount { get; set; }
        public decimal TotalSoh { get; set; }
        public decimal CurrentStockValue { get; set; }
        public decimal CurrentRetailValue { get; set; }
        public decimal CurrentWholesaleValue { get; set; }
        public DateTime? LastReceivedDate { get; set; }
        public DateTime? EarliestExpiryDate { get; set; }
        public decimal MovingAverageCost { get; set; }
        public decimal LastLandedCost { get; set; }

        public decimal RetailPrice
        {
            get => _retailPrice;
            set => SetPrice(ref _retailPrice, value, nameof(RetailPrice));
        }

        public decimal WholesalePrice
        {
            get => _wholesalePrice;
            set => SetPrice(ref _wholesalePrice, value, nameof(WholesalePrice));
        }

        public decimal MinimumPrice
        {
            get => _minimumPrice;
            set => SetPrice(ref _minimumPrice, value, nameof(MinimumPrice));
        }

        public decimal MaximumPrice
        {
            get => _maximumPrice;
            set => SetPrice(ref _maximumPrice, value, nameof(MaximumPrice));
        }

        public decimal OriginalRetailPrice { get; private set; }
        public decimal OriginalWholesalePrice { get; private set; }
        public decimal OriginalMinimumPrice { get; private set; }
        public decimal OriginalMaximumPrice { get; private set; }

        public bool IsDirty
        {
            get => _isDirty;
            private set
            {
                if (_isDirty == value)
                    return;

                _isDirty = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasMasterPriceChanged));
            }
        }

        public bool IsService =>
            string.Equals(ItemType, ItemTypeCodes.Service, StringComparison.Ordinal);

        public string ItemTypeText => IsService ? "Service" : "Stock Item";

        public string DisplayDescription =>
            string.IsNullOrWhiteSpace(VariantAttributes) ||
            VariantAttributes.Equals("Standard", StringComparison.OrdinalIgnoreCase)
                ? Description
                : $"{Description} - {VariantAttributes}";

        public string TrackingText
        {
            get
            {
                if (IsService)
                    return "Service / No Stock";
                if (!HasBatchTracking)
                    return "Average Cost";
                return HasExpiryTracking ? "Batch + Expiry" : "Batch";
            }
        }

        public string CostMethodText =>
            IsService ? "Standard Cost" :
            HasBatchTracking ? "Batch Weighted Cost" : "Average Cost";

        public decimal EffectiveCost => MovingAverageCost > 0m
            ? RoundMoney(MovingAverageCost)
            : RoundMoney(LastLandedCost);

        public string BatchPriceStatus => BatchOverrideCount switch
        {
            <= 0 => "Master Price",
            1 => "1 Batch Override",
            _ => $"{BatchOverrideCount} Batch Overrides"
        };

        public bool CanManageBatchOverrides =>
            !IsService && HasBatchTracking;

        public string BatchEditorMessage => CanManageBatchOverrides
            ? "Select a physical batch to manage its Retail and Wholesale override."
            : IsService
                ? "Services use master prices only."
                : "This Stock Item uses the GENERAL stock bucket and master prices only.";

        public bool HasMasterPriceChanged =>
            RetailPrice != OriginalRetailPrice ||
            WholesalePrice != OriginalWholesalePrice ||
            MinimumPrice != OriginalMinimumPrice ||
            MaximumPrice != OriginalMaximumPrice;

        // Compatibility properties retained for existing audit and reporting code.
        public bool HasInventory => !IsService;
        public decimal? InventoryStockOnHand => IsService ? null : TotalSoh;
        public decimal? InventoryStockValue => IsService ? null : CurrentStockValue;
        public decimal NewRetailValue => Math.Round(TotalSoh * RetailPrice, 2);
        public decimal? InventoryRetailValue => IsService ? null : NewRetailValue;
        public decimal NewWholesaleValue => Math.Round(TotalSoh * WholesalePrice, 2);
        public string ActiveStockRowsText => IsService ? "—" : ActiveStockRowCount <= 0 ? "-" : HasBatchTracking ? ActiveStockRowCount.ToString() : "GENERAL";
        public decimal GrossMarginPercentage => RetailPrice > 0m && EffectiveCost > 0m ? Math.Round(((RetailPrice - EffectiveCost) / RetailPrice) * 100m, 2) : 0m;
        public decimal WholesaleMarginPercentage => WholesalePrice > 0m && EffectiveCost > 0m ? Math.Round(((WholesalePrice - EffectiveCost) / WholesalePrice) * 100m, 2) : 0m;
        public bool IsCostMissing => EffectiveCost <= 0m;
        public bool IsNegativeMargin => RetailPrice > 0m && EffectiveCost > 0m && RetailPrice < EffectiveCost;
        public bool IsLowMargin => RetailPrice > 0m && EffectiveCost > 0m && RetailPrice >= EffectiveCost && GrossMarginPercentage < 20m;
        public bool IsBelowMinimumPrice => MinimumPrice > 0m && RetailPrice > 0m && RetailPrice < MinimumPrice;
        public bool IsWholesaleBelowMinimumPrice => MinimumPrice > 0m && WholesalePrice > 0m && WholesalePrice < MinimumPrice;
        public bool IsAboveMaximumPrice => MaximumPrice > 0m && RetailPrice > MaximumPrice;
        public bool IsWholesaleAboveMaximumPrice => MaximumPrice > 0m && WholesalePrice > MaximumPrice;
        public string MarginHealth => RetailPrice <= 0m ? "No Retail Price" : IsCostMissing ? "Cost Missing" : IsNegativeMargin ? "Negative Margin" : IsBelowMinimumPrice ? "Below Minimum" : IsAboveMaximumPrice ? "Above Maximum" : IsLowMargin ? "Low Margin" : "Healthy";

        public void AcceptChanges()
        {
            _suppressDirtyTracking = true;
            OriginalRetailPrice = RetailPrice;
            OriginalWholesalePrice = WholesalePrice;
            OriginalMinimumPrice = MinimumPrice;
            OriginalMaximumPrice = MaximumPrice;
            IsDirty = false;
            _suppressDirtyTracking = false;
        }

        public void ResetChanges()
        {
            _suppressDirtyTracking = true;
            RetailPrice = OriginalRetailPrice;
            WholesalePrice = OriginalWholesalePrice;
            MinimumPrice = OriginalMinimumPrice;
            MaximumPrice = OriginalMaximumPrice;
            IsDirty = false;
            _suppressDirtyTracking = false;
        }

        public List<string> ValidateForSave()
        {
            var errors = new List<string>();
            if (ItemVariantId <= 0)
                errors.Add("Invalid item variant.");
            if (RetailPrice <= 0m)
                errors.Add($"Retail price must be greater than zero for '{DisplayDescription}'.");
            if (WholesalePrice < 0m || MinimumPrice < 0m || MaximumPrice < 0m)
                errors.Add($"Prices cannot be negative for '{DisplayDescription}'.");
            if (WholesalePrice > RetailPrice)
                errors.Add($"Wholesale price cannot be greater than Retail price for '{DisplayDescription}'.");
            if (MinimumPrice > 0m && MaximumPrice > 0m && MinimumPrice > MaximumPrice)
                errors.Add($"Minimum price cannot be greater than Maximum price for '{DisplayDescription}'.");
            if (MinimumPrice > 0m && RetailPrice < MinimumPrice)
                errors.Add($"Retail price cannot be below Minimum price for '{DisplayDescription}'.");
            if (MinimumPrice > 0m && WholesalePrice > 0m && WholesalePrice < MinimumPrice)
                errors.Add($"Wholesale price cannot be below Minimum price for '{DisplayDescription}'.");
            if (MaximumPrice > 0m && RetailPrice > MaximumPrice)
                errors.Add($"Retail price cannot be above Maximum price for '{DisplayDescription}'.");
            if (MaximumPrice > 0m && WholesalePrice > MaximumPrice)
                errors.Add($"Wholesale price cannot be above Maximum price for '{DisplayDescription}'.");
            return errors;
        }

        private void SetPrice(ref decimal field, decimal value, string propertyName)
        {
            decimal rounded = RoundMoney(value);
            if (field == rounded)
                return;
            field = rounded;
            if (!_suppressDirtyTracking)
                IsDirty = true;
            OnPropertyChanged(propertyName);
            OnPropertyChanged(nameof(HasMasterPriceChanged));
            OnPropertyChanged(nameof(NewRetailValue));
            OnPropertyChanged(nameof(NewWholesaleValue));
            OnPropertyChanged(nameof(InventoryRetailValue));
            OnPropertyChanged(nameof(GrossMarginPercentage));
            OnPropertyChanged(nameof(WholesaleMarginPercentage));
            OnPropertyChanged(nameof(MarginHealth));
        }

        private static decimal RoundMoney(decimal value) => Math.Round(value, 2);

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class PriceManagementBatchDto : INotifyPropertyChanged
    {
        private decimal _overrideRetailPrice;
        private decimal _overrideWholesalePrice;

        public int ItemBatchId { get; set; }
        public int ItemVariantId { get; set; }
        public string BatchNo { get; set; } = string.Empty;
        public string InternalBatchBarcode { get; set; } = string.Empty;
        public bool IsGeneralStockBucket { get; set; }
        public bool IsDeactivated { get; set; }
        public bool IsOverrideEligible { get; set; }
        public bool HasSellingPriceOverride { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public DateTime ReceivedDate { get; set; }
        public decimal CurrentStock { get; set; }
        public decimal CostPrice { get; set; }
        public decimal StoredRetailPrice { get; set; }
        public decimal StoredWholesalePrice { get; set; }
        public decimal EffectiveRetailPrice { get; set; }
        public decimal EffectiveWholesalePrice { get; set; }
        public string PriceSource { get; set; } = SellingPriceSourceCodes.Master;
        public decimal AverageCost { get; set; }
        public decimal LastCost { get; set; }
        public decimal MasterMinimumPrice { get; set; }
        public decimal MasterRetailPrice { get; set; }
        public decimal MasterWholesalePrice { get; set; }
        public decimal MasterMaximumPrice { get; set; }

        public decimal OverrideRetailPrice
        {
            get => _overrideRetailPrice;
            set
            {
                decimal rounded = Math.Round(value, 2);
                if (_overrideRetailPrice == rounded)
                    return;
                _overrideRetailPrice = rounded;
                OnPropertyChanged();
                NotifyProposedPriceProperties();
            }
        }

        public decimal OverrideWholesalePrice
        {
            get => _overrideWholesalePrice;
            set
            {
                decimal rounded = Math.Round(value, 2);
                if (_overrideWholesalePrice == rounded)
                    return;
                _overrideWholesalePrice = rounded;
                OnPropertyChanged();
                NotifyProposedPriceProperties();
            }
        }

        public string PriceSourceText =>
            PriceSource == SellingPriceSourceCodes.BatchOverride
                ? "Batch Override"
                : "Master Price";

        public string OverrideActionText => HasSellingPriceOverride
            ? "UPDATE BATCH PRICE OVERRIDE"
            : "SET BATCH PRICE OVERRIDE";

        public string ExpiryDisplayText => ExpiryDate.HasValue
            ? ExpiryDate.Value.ToString("yyyy-MM-dd")
            : "No Expiry";

        // Compatibility aliases.
        public decimal RetailPrice { get => EffectiveRetailPrice; set => OverrideRetailPrice = value; }
        public decimal WholesalePrice { get => EffectiveWholesalePrice; set => OverrideWholesalePrice = value; }
        public decimal EffectiveCost => CostPrice;
        public decimal OriginalRetailPrice { get; private set; }
        public decimal OriginalWholesalePrice { get; private set; }
        public bool IsDirty => OverrideRetailPrice != OriginalRetailPrice || OverrideWholesalePrice != OriginalWholesalePrice;
        public bool HasBatchPriceChanged => IsDirty;
        public decimal RetailMarginPercentage => EffectiveRetailPrice > 0m && CostPrice > 0m ? Math.Round(((EffectiveRetailPrice - CostPrice) / EffectiveRetailPrice) * 100m, 2) : 0m;
        public decimal WholesaleMarginPercentage => EffectiveWholesalePrice > 0m && CostPrice > 0m ? Math.Round(((EffectiveWholesalePrice - CostPrice) / EffectiveWholesalePrice) * 100m, 2) : 0m;
        public decimal ProposedRetailProfit => SellingPriceSuggestionCalculator.ProfitPerUnit(OverrideRetailPrice, CostPrice);
        public decimal ProposedWholesaleProfit => SellingPriceSuggestionCalculator.ProfitPerUnit(OverrideWholesalePrice, CostPrice);
        public decimal ProposedRetailMarginPercentage => SellingPriceSuggestionCalculator.MarginPercent(OverrideRetailPrice, CostPrice);
        public decimal ProposedWholesaleMarginPercentage => SellingPriceSuggestionCalculator.MarginPercent(OverrideWholesalePrice, CostPrice);
        public bool IsProposedRetailBelowCost => CostPrice > 0m && OverrideRetailPrice > 0m && OverrideRetailPrice < CostPrice;
        public bool IsProposedWholesaleBelowCost => CostPrice > 0m && OverrideWholesalePrice > 0m && OverrideWholesalePrice < CostPrice;
        public string ProposedCostWarning => IsProposedRetailBelowCost || IsProposedWholesaleBelowCost
            ? "Warning: one or more proposed batch prices are below Batch Cost."
            : string.Empty;
        public bool IsNegativeMargin => EffectiveRetailPrice > 0m && CostPrice > 0m && EffectiveRetailPrice < CostPrice;
        public bool IsLowMargin => EffectiveRetailPrice > 0m && CostPrice > 0m && EffectiveRetailPrice >= CostPrice && RetailMarginPercentage < 20m;
        public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value.Date < DateTime.Today;
        public bool IsExpiringSoon => ExpiryDate.HasValue && ExpiryDate.Value.Date >= DateTime.Today && ExpiryDate.Value.Date <= DateTime.Today.AddDays(30);
        public string ExpiryStatus => !ExpiryDate.HasValue ? "No Expiry" : IsExpired ? "Expired" : IsExpiringSoon ? "Expiring Soon" : "Normal";
        public string MarginHealth => EffectiveRetailPrice <= 0m ? "No Retail Price" : CostPrice <= 0m ? "Cost Missing" : IsNegativeMargin ? "Negative Margin" : IsLowMargin ? "Low Margin" : "Healthy";

        public void InitializeEditor()
        {
            OverrideRetailPrice = HasSellingPriceOverride ? StoredRetailPrice : EffectiveRetailPrice;
            OverrideWholesalePrice = HasSellingPriceOverride ? StoredWholesalePrice : EffectiveWholesalePrice;
            OriginalRetailPrice = OverrideRetailPrice;
            OriginalWholesalePrice = OverrideWholesalePrice;
            NotifyProposedPriceProperties();
        }

        public void AcceptChanges()
        {
            OriginalRetailPrice = OverrideRetailPrice;
            OriginalWholesalePrice = OverrideWholesalePrice;
            NotifyProposedPriceProperties();
        }

        public void ResetEditor()
        {
            OverrideRetailPrice = HasSellingPriceOverride ? StoredRetailPrice : EffectiveRetailPrice;
            OverrideWholesalePrice = HasSellingPriceOverride ? StoredWholesalePrice : EffectiveWholesalePrice;
            AcceptChanges();
        }

        public List<string> ValidateForSave()
        {
            var errors = new List<string>();
            if (ItemBatchId <= 0)
                errors.Add("Invalid batch.");
            if (ItemVariantId <= 0)
                errors.Add("Invalid item variant.");
            if (!IsOverrideEligible)
                errors.Add("The selected stock bucket cannot use a batch override.");
            if (OverrideRetailPrice <= 0m)
                errors.Add($"Retail price must be greater than zero for batch '{BatchNo}'.");
            if (OverrideWholesalePrice <= 0m)
                errors.Add($"Wholesale price must be greater than zero for batch '{BatchNo}'.");
            if (OverrideWholesalePrice > OverrideRetailPrice)
                errors.Add($"Wholesale price cannot be greater than Retail price for batch '{BatchNo}'.");
            if (MasterMinimumPrice > 0m && OverrideRetailPrice < MasterMinimumPrice)
                errors.Add($"Retail price cannot be below the master Minimum price for batch '{BatchNo}'.");
            if (MasterMinimumPrice > 0m && OverrideWholesalePrice < MasterMinimumPrice)
                errors.Add($"Wholesale price cannot be below the master Minimum price for batch '{BatchNo}'.");
            if (MasterMaximumPrice > 0m && OverrideRetailPrice > MasterMaximumPrice)
                errors.Add($"Retail price cannot be above the master Maximum price for batch '{BatchNo}'.");
            if (MasterMaximumPrice > 0m && OverrideWholesalePrice > MasterMaximumPrice)
                errors.Add($"Wholesale price cannot be above the master Maximum price for batch '{BatchNo}'.");
            return errors;
        }


        private void NotifyProposedPriceProperties()
        {
            OnPropertyChanged(nameof(IsDirty));
            OnPropertyChanged(nameof(HasBatchPriceChanged));
            OnPropertyChanged(nameof(ProposedRetailProfit));
            OnPropertyChanged(nameof(ProposedWholesaleProfit));
            OnPropertyChanged(nameof(ProposedRetailMarginPercentage));
            OnPropertyChanged(nameof(ProposedWholesaleMarginPercentage));
            OnPropertyChanged(nameof(IsProposedRetailBelowCost));
            OnPropertyChanged(nameof(IsProposedWholesaleBelowCost));
            OnPropertyChanged(nameof(ProposedCostWarning));
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
