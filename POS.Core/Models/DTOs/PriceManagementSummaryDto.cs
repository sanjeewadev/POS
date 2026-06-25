using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace POS.Core.Models.DTOs
{
    public class PriceManagementSummaryDto : INotifyPropertyChanged
    {
        public int ItemVariantId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string VariantAttributes { get; set; } = string.Empty;

        public decimal TotalSoh { get; set; }
        public decimal MovingAverageCost { get; set; } // The true cost (MAC)
        public decimal LastLandedCost { get; set; }    // The vendor's last invoice cost

        private decimal _retailPrice;
        public decimal RetailPrice
        {
            get => _retailPrice;
            set
            {
                if (_retailPrice != value)
                {
                    _retailPrice = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(GrossMarginPercentage)); // Auto-update margin UI
                }
            }
        }

        private decimal _wholesalePrice;
        public decimal WholesalePrice
        {
            get => _wholesalePrice;
            set
            {
                if (_wholesalePrice != value)
                {
                    _wholesalePrice = value;
                    OnPropertyChanged();
                }
            }
        }

        private decimal _minimumPrice;
        public decimal MinimumPrice
        {
            get => _minimumPrice;
            set
            {
                if (_minimumPrice != value)
                {
                    _minimumPrice = value;
                    OnPropertyChanged();
                }
            }
        }

        // Calculates instantly: ((Retail - Cost) / Retail) * 100
        public decimal GrossMarginPercentage
        {
            get
            {
                if (RetailPrice <= 0) return 0m;
                return Math.Round(((RetailPrice - MovingAverageCost) / RetailPrice) * 100, 2);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}