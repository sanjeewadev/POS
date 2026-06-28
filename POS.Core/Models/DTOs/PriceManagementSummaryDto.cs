using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

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

        public string ItemCode { get; set; } = string.Empty;
        public string SkuCode { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
        public string VariantAttributes { get; set; } = string.Empty;

        public decimal TotalSoh { get; set; }

        public decimal MovingAverageCost
        {
            get => _movingAverageCost;
            set
            {
                if (_movingAverageCost != value)
                {
                    _movingAverageCost = value;
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
                if (_lastLandedCost != value)
                {
                    _lastLandedCost = value;
                    OnPropertyChanged();
                    NotifyMarginProperties();
                }
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

        public decimal GrossMarginPercentage
        {
            get
            {
                if (RetailPrice <= 0)
                    return 0m;

                return Math.Round(((RetailPrice - MovingAverageCost) / RetailPrice) * 100m, 2);
            }
        }

        public decimal WholesaleMarginPercentage
        {
            get
            {
                if (WholesalePrice <= 0)
                    return 0m;

                return Math.Round(((WholesalePrice - MovingAverageCost) / WholesalePrice) * 100m, 2);
            }
        }

        public bool IsNegativeMargin => RetailPrice > 0 && RetailPrice < MovingAverageCost;

        public bool IsLowMargin => RetailPrice > 0 &&
                                   RetailPrice >= MovingAverageCost &&
                                   GrossMarginPercentage < 20m;

        public bool IsBelowMinimumPrice => MinimumPrice > 0 && RetailPrice < MinimumPrice;

        public bool IsAboveMaximumPrice => MaximumPrice > 0 && RetailPrice > MaximumPrice;

        public string MarginHealth
        {
            get
            {
                if (RetailPrice <= 0)
                    return "No Retail Price";

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
        }

        public List<string> ValidateForSave()
        {
            var errors = new List<string>();

            if (ItemVariantId <= 0)
                errors.Add("Invalid item variant.");

            if (RetailPrice < 0)
                errors.Add("Retail price cannot be negative.");

            if (WholesalePrice < 0)
                errors.Add("Wholesale price cannot be negative.");

            if (MinimumPrice < 0)
                errors.Add("Minimum price cannot be negative.");

            if (MaximumPrice < 0)
                errors.Add("Maximum price cannot be negative.");

            if (RetailPrice <= 0)
                errors.Add("Retail price must be greater than zero.");

            if (MinimumPrice > 0 && RetailPrice < MinimumPrice)
                errors.Add("Retail price cannot be lower than minimum price.");

            if (MinimumPrice > 0 && WholesalePrice > 0 && WholesalePrice < MinimumPrice)
                errors.Add("Wholesale price cannot be lower than minimum price.");

            if (MaximumPrice > 0 && RetailPrice > MaximumPrice)
                errors.Add("Retail price cannot be higher than maximum price.");

            if (MaximumPrice > 0 && WholesalePrice > MaximumPrice)
                errors.Add("Wholesale price cannot be higher than maximum price.");

            return errors;
        }

        private void MarkDirty()
        {
            if (!_suppressDirtyTracking)
                IsDirty = true;
        }

        private void NotifyMarginProperties()
        {
            OnPropertyChanged(nameof(GrossMarginPercentage));
            OnPropertyChanged(nameof(WholesaleMarginPercentage));
            OnPropertyChanged(nameof(IsNegativeMargin));
            OnPropertyChanged(nameof(IsLowMargin));
            OnPropertyChanged(nameof(MarginHealth));
        }

        private void NotifyRuleProperties()
        {
            OnPropertyChanged(nameof(IsBelowMinimumPrice));
            OnPropertyChanged(nameof(IsAboveMaximumPrice));
            OnPropertyChanged(nameof(MarginHealth));
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

    public class PriceManagementBatchDto : INotifyPropertyChanged
    {
        private bool _suppressDirtyTracking = false;

        private decimal _retailPrice;
        private decimal _wholesalePrice;
        private bool _isDirty;

        public int ItemBatchId { get; set; }
        public int ItemVariantId { get; set; }

        public string BatchNo { get; set; } = string.Empty;

        public DateTime? ExpiryDate { get; set; }
        public DateTime ReceivedDate { get; set; }

        public decimal CurrentStock { get; set; }
        public decimal CostPrice { get; set; }

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

        public decimal RetailMarginPercentage
        {
            get
            {
                if (RetailPrice <= 0)
                    return 0m;

                return Math.Round(((RetailPrice - CostPrice) / RetailPrice) * 100m, 2);
            }
        }

        public decimal WholesaleMarginPercentage
        {
            get
            {
                if (WholesalePrice <= 0)
                    return 0m;

                return Math.Round(((WholesalePrice - CostPrice) / WholesalePrice) * 100m, 2);
            }
        }

        public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value.Date < DateTime.Today;

        public bool IsExpiringSoon => ExpiryDate.HasValue &&
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
            OnPropertyChanged(nameof(RetailMarginPercentage));
            OnPropertyChanged(nameof(WholesaleMarginPercentage));
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