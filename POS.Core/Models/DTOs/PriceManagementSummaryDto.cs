using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using POS.Core.Configuration;

namespace POS.Core.Models.DTOs
{
    public class PriceManagementSummaryDto : INotifyPropertyChanged
    {
        private bool _suppressDirtyTracking = false;

        private decimal _movingAverageCost;
        private decimal _lastLandedCost;
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

        public bool IsService =>
            string.Equals(
                ItemType,
                ItemTypeCodes.Service,
                StringComparison.Ordinal);

        public string ItemTypeText => IsService ? "Service" : "Stock Item";

        public bool HasInventory => !IsService;

        public bool HasBatchTracking { get; set; }
        public bool HasExpiryTracking { get; set; }

        public int ActiveStockRowCount { get; set; }
        public decimal TotalSoh { get; set; }

        public decimal CurrentStockValue { get; set; }
        public decimal CurrentRetailValue { get; set; }
        public decimal CurrentWholesaleValue { get; set; }

        public DateTime? LastReceivedDate { get; set; }
        public DateTime? EarliestExpiryDate { get; set; }

        public decimal MovingAverageCost
        {
            get => _movingAverageCost;
            set
            {
                decimal newValue = RoundMoney(value);

                if (_movingAverageCost != newValue)
                {
                    _movingAverageCost = newValue;
                    OnPropertyChanged();
                    NotifyMarginProperties();
                }
            }
        }

        public decimal LastLandedCost
        {
            get => _lastLandedCost;
            set
            {
                decimal newValue = RoundMoney(value);

                if (_lastLandedCost != newValue)
                {
                    _lastLandedCost = newValue;
                    OnPropertyChanged();
                    NotifyMarginProperties();
                }
            }
        }

        public decimal EffectiveCost
        {
            get
            {
                if (MovingAverageCost > 0)
                    return MovingAverageCost;

                if (LastLandedCost > 0)
                    return LastLandedCost;

                return 0m;
            }
        }

        public decimal RetailPrice
        {
            get => _retailPrice;
            set
            {
                decimal newValue = RoundMoney(value);

                if (_retailPrice != newValue)
                {
                    _retailPrice = newValue;
                    MarkDirty();
                    OnPropertyChanged();
                    NotifyMarginProperties();
                    NotifyRuleProperties();
                    NotifyValueProperties();
                }
            }
        }

        public decimal WholesalePrice
        {
            get => _wholesalePrice;
            set
            {
                decimal newValue = RoundMoney(value);

                if (_wholesalePrice != newValue)
                {
                    _wholesalePrice = newValue;
                    MarkDirty();
                    OnPropertyChanged();
                    NotifyMarginProperties();
                    NotifyRuleProperties();
                    NotifyValueProperties();
                }
            }
        }

        public decimal MinimumPrice
        {
            get => _minimumPrice;
            set
            {
                decimal newValue = RoundMoney(value);

                if (_minimumPrice != newValue)
                {
                    _minimumPrice = newValue;
                    MarkDirty();
                    OnPropertyChanged();
                    NotifyRuleProperties();
                    NotifyMarginProperties();
                }
            }
        }

        public decimal MaximumPrice
        {
            get => _maximumPrice;
            set
            {
                decimal newValue = RoundMoney(value);

                if (_maximumPrice != newValue)
                {
                    _maximumPrice = newValue;
                    MarkDirty();
                    OnPropertyChanged();
                    NotifyRuleProperties();
                    NotifyMarginProperties();
                }
            }
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
                if (_isDirty != value)
                {
                    _isDirty = value;
                    OnPropertyChanged();
                }
            }
        }

        public string DisplayDescription
        {
            get
            {
                if (string.IsNullOrWhiteSpace(VariantAttributes) ||
                    VariantAttributes.Equals("Standard", StringComparison.OrdinalIgnoreCase))
                {
                    return Description;
                }

                return $"{Description} - {VariantAttributes}";
            }
        }

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

        public string CostMethodText
        {
            get
            {
                if (IsService)
                    return "Standard Cost";

                if (!HasBatchTracking)
                    return "Average Cost";

                return "Batch Weighted Cost";
            }
        }

        public string ActiveStockRowsText
        {
            get
            {
                if (IsService)
                    return "—";

                if (ActiveStockRowCount <= 0)
                    return "-";

                if (!HasBatchTracking)
                    return "GENERAL";

                return ActiveStockRowCount.ToString();
            }
        }

        public decimal? InventoryStockOnHand => IsService ? null : TotalSoh;

        public decimal? InventoryStockValue => IsService ? null : CurrentStockValue;

        public decimal NewRetailValue => Math.Round(TotalSoh * RetailPrice, 2);

        public decimal? InventoryRetailValue => IsService ? null : NewRetailValue;

        public decimal NewWholesaleValue => Math.Round(TotalSoh * WholesalePrice, 2);

        public decimal GrossMarginPercentage
        {
            get
            {
                if (RetailPrice <= 0 || EffectiveCost <= 0)
                    return 0m;

                return Math.Round(((RetailPrice - EffectiveCost) / RetailPrice) * 100m, 2);
            }
        }

        public decimal WholesaleMarginPercentage
        {
            get
            {
                if (WholesalePrice <= 0 || EffectiveCost <= 0)
                    return 0m;

                return Math.Round(((WholesalePrice - EffectiveCost) / WholesalePrice) * 100m, 2);
            }
        }

        public bool IsCostMissing => EffectiveCost <= 0;

        public bool IsNegativeMargin =>
            RetailPrice > 0 &&
            EffectiveCost > 0 &&
            RetailPrice < EffectiveCost;

        public bool IsLowMargin =>
            RetailPrice > 0 &&
            EffectiveCost > 0 &&
            RetailPrice >= EffectiveCost &&
            GrossMarginPercentage < 20m;

        public bool IsBelowMinimumPrice =>
            MinimumPrice > 0 &&
            RetailPrice > 0 &&
            RetailPrice < MinimumPrice;

        public bool IsWholesaleBelowMinimumPrice =>
            MinimumPrice > 0 &&
            WholesalePrice > 0 &&
            WholesalePrice < MinimumPrice;

        public bool IsAboveMaximumPrice =>
            MaximumPrice > 0 &&
            RetailPrice > MaximumPrice;

        public bool IsWholesaleAboveMaximumPrice =>
            MaximumPrice > 0 &&
            WholesalePrice > MaximumPrice;

        public bool HasMasterPriceChanged =>
            RetailPrice != OriginalRetailPrice ||
            WholesalePrice != OriginalWholesalePrice ||
            MinimumPrice != OriginalMinimumPrice ||
            MaximumPrice != OriginalMaximumPrice;

        public string MarginHealth
        {
            get
            {
                if (RetailPrice <= 0)
                    return "No Retail Price";

                if (IsCostMissing)
                    return "Cost Missing";

                if (IsNegativeMargin)
                    return "Negative Margin";

                if (IsBelowMinimumPrice)
                    return "Below Minimum";

                if (IsAboveMaximumPrice)
                    return "Above Maximum";

                if (IsLowMargin)
                    return "Low Margin";

                return "Healthy";
            }
        }

        public void AcceptChanges()
        {
            _suppressDirtyTracking = true;

            OriginalRetailPrice = RetailPrice;
            OriginalWholesalePrice = WholesalePrice;
            OriginalMinimumPrice = MinimumPrice;
            OriginalMaximumPrice = MaximumPrice;

            IsDirty = false;

            _suppressDirtyTracking = false;

            OnPropertyChanged(nameof(HasMasterPriceChanged));
        }

        public List<string> ValidateForSave()
        {
            var errors = new List<string>();

            if (ItemVariantId <= 0)
                errors.Add("Invalid item variant.");

            if (RetailPrice < 0)
                errors.Add($"Retail price cannot be negative for '{ItemCode}'.");

            if (WholesalePrice < 0)
                errors.Add($"Wholesale price cannot be negative for '{ItemCode}'.");

            if (MinimumPrice < 0)
                errors.Add($"Minimum price cannot be negative for '{ItemCode}'.");

            if (MaximumPrice < 0)
                errors.Add($"Maximum price cannot be negative for '{ItemCode}'.");

            if (RetailPrice <= 0)
                errors.Add($"Retail price must be greater than zero for '{ItemCode}'.");

            if (MaximumPrice > 0 &&
                MinimumPrice > 0 &&
                MaximumPrice < MinimumPrice)
            {
                errors.Add($"Maximum price cannot be lower than minimum price for '{ItemCode}'.");
            }

            if (MinimumPrice > 0 &&
                RetailPrice > 0 &&
                RetailPrice < MinimumPrice)
            {
                errors.Add($"Retail price cannot be lower than minimum price for '{ItemCode}'.");
            }

            if (MinimumPrice > 0 &&
                WholesalePrice > 0 &&
                WholesalePrice < MinimumPrice)
            {
                errors.Add($"Wholesale price cannot be lower than minimum price for '{ItemCode}'.");
            }

            if (MaximumPrice > 0 &&
                RetailPrice > MaximumPrice)
            {
                errors.Add($"Retail price cannot be higher than maximum price for '{ItemCode}'.");
            }

            if (MaximumPrice > 0 &&
                WholesalePrice > MaximumPrice)
            {
                errors.Add($"Wholesale price cannot be higher than maximum price for '{ItemCode}'.");
            }

            return errors;
        }

        private void MarkDirty()
        {
            if (!_suppressDirtyTracking)
                IsDirty = true;
        }

        private void NotifyMarginProperties()
        {
            OnPropertyChanged(nameof(EffectiveCost));
            OnPropertyChanged(nameof(GrossMarginPercentage));
            OnPropertyChanged(nameof(WholesaleMarginPercentage));
            OnPropertyChanged(nameof(IsCostMissing));
            OnPropertyChanged(nameof(IsNegativeMargin));
            OnPropertyChanged(nameof(IsLowMargin));
            OnPropertyChanged(nameof(MarginHealth));
        }

        private void NotifyRuleProperties()
        {
            OnPropertyChanged(nameof(IsBelowMinimumPrice));
            OnPropertyChanged(nameof(IsWholesaleBelowMinimumPrice));
            OnPropertyChanged(nameof(IsAboveMaximumPrice));
            OnPropertyChanged(nameof(IsWholesaleAboveMaximumPrice));
            OnPropertyChanged(nameof(HasMasterPriceChanged));
            OnPropertyChanged(nameof(MarginHealth));
        }

        private void NotifyValueProperties()
        {
            OnPropertyChanged(nameof(NewRetailValue));
            OnPropertyChanged(nameof(NewWholesaleValue));
            OnPropertyChanged(nameof(InventoryRetailValue));
        }

        private static decimal RoundMoney(decimal value)
        {
            return Math.Round(value, 2);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Kept for compatibility with old code. The simplified Price Management page does not edit batches separately.
    public class PriceManagementBatchDto : INotifyPropertyChanged
    {
        private bool _suppressDirtyTracking = false;

        private decimal _retailPrice;
        private decimal _wholesalePrice;
        private bool _isDirty;

        public int ItemBatchId { get; set; }
        public int ItemVariantId { get; set; }

        public string BatchNo { get; set; } = string.Empty;
        public string InternalBatchBarcode { get; set; } = string.Empty;

        public bool IsGeneralStockBucket { get; set; }

        public DateTime? ExpiryDate { get; set; }
        public DateTime ReceivedDate { get; set; }

        public decimal CurrentStock { get; set; }
        public decimal CostPrice { get; set; }

        public decimal EffectiveCost => CostPrice;

        public decimal RetailPrice
        {
            get => _retailPrice;
            set
            {
                decimal newValue = RoundMoney(value);

                if (_retailPrice != newValue)
                {
                    _retailPrice = newValue;
                    MarkDirty();
                    OnPropertyChanged();
                    NotifyMarginProperties();
                }
            }
        }

        public decimal WholesalePrice
        {
            get => _wholesalePrice;
            set
            {
                decimal newValue = RoundMoney(value);

                if (_wholesalePrice != newValue)
                {
                    _wholesalePrice = newValue;
                    MarkDirty();
                    OnPropertyChanged();
                    NotifyMarginProperties();
                }
            }
        }

        public decimal OriginalRetailPrice { get; private set; }
        public decimal OriginalWholesalePrice { get; private set; }

        public bool IsDirty
        {
            get => _isDirty;
            private set
            {
                if (_isDirty != value)
                {
                    _isDirty = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HasBatchPriceChanged =>
            RetailPrice != OriginalRetailPrice ||
            WholesalePrice != OriginalWholesalePrice;

        public decimal RetailMarginPercentage
        {
            get
            {
                if (RetailPrice <= 0 || CostPrice <= 0)
                    return 0m;

                return Math.Round(((RetailPrice - CostPrice) / RetailPrice) * 100m, 2);
            }
        }

        public decimal WholesaleMarginPercentage
        {
            get
            {
                if (WholesalePrice <= 0 || CostPrice <= 0)
                    return 0m;

                return Math.Round(((WholesalePrice - CostPrice) / WholesalePrice) * 100m, 2);
            }
        }

        public bool IsNegativeMargin =>
            RetailPrice > 0 &&
            CostPrice > 0 &&
            RetailPrice < CostPrice;

        public bool IsLowMargin =>
            RetailPrice > 0 &&
            CostPrice > 0 &&
            RetailPrice >= CostPrice &&
            RetailMarginPercentage < 20m;

        public bool IsExpired =>
            ExpiryDate.HasValue &&
            ExpiryDate.Value.Date < DateTime.Today;

        public bool IsExpiringSoon =>
            ExpiryDate.HasValue &&
            ExpiryDate.Value.Date >= DateTime.Today &&
            ExpiryDate.Value.Date <= DateTime.Today.AddDays(30);

        public string ExpiryStatus
        {
            get
            {
                if (!ExpiryDate.HasValue)
                    return "No Expiry";

                if (IsExpired)
                    return "Expired";

                if (IsExpiringSoon)
                    return "Expiring Soon";

                return "Normal";
            }
        }

        public string MarginHealth
        {
            get
            {
                if (RetailPrice <= 0)
                    return "No Retail Price";

                if (CostPrice <= 0)
                    return "Cost Missing";

                if (IsNegativeMargin)
                    return "Negative Margin";

                if (IsLowMargin)
                    return "Low Margin";

                return "Healthy";
            }
        }

        public void AcceptChanges()
        {
            _suppressDirtyTracking = true;

            OriginalRetailPrice = RetailPrice;
            OriginalWholesalePrice = WholesalePrice;

            IsDirty = false;

            _suppressDirtyTracking = false;
        }

        public List<string> ValidateForSave()
        {
            var errors = new List<string>();

            if (ItemBatchId <= 0)
                errors.Add("Invalid batch.");

            if (ItemVariantId <= 0)
                errors.Add("Invalid item variant.");

            if (RetailPrice < 0)
                errors.Add($"Retail price cannot be negative for batch '{BatchNo}'.");

            if (WholesalePrice < 0)
                errors.Add($"Wholesale price cannot be negative for batch '{BatchNo}'.");

            if (RetailPrice <= 0)
                errors.Add($"Retail price must be greater than zero for batch '{BatchNo}'.");

            return errors;
        }

        private void MarkDirty()
        {
            if (!_suppressDirtyTracking)
                IsDirty = true;
        }

        private void NotifyMarginProperties()
        {
            OnPropertyChanged(nameof(EffectiveCost));
            OnPropertyChanged(nameof(RetailMarginPercentage));
            OnPropertyChanged(nameof(WholesaleMarginPercentage));
            OnPropertyChanged(nameof(IsNegativeMargin));
            OnPropertyChanged(nameof(IsLowMargin));
            OnPropertyChanged(nameof(MarginHealth));
            OnPropertyChanged(nameof(HasBatchPriceChanged));
        }

        private static decimal RoundMoney(decimal value)
        {
            return Math.Round(value, 2);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
