using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace POS.Cashier.UI.Models
{
    public partial class CartItem : ObservableObject
    {
        // Real sellable item identity.
        public int ItemVariantId { get; set; }

        // Backward-compatible alias for old bindings/code.
        public int ItemId
        {
            get => ItemVariantId;
            set => ItemVariantId = value;
        }

        public string ItemCode { get; set; } = string.Empty;
        public string SkuCode { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
        public string VariantDescription { get; set; } = string.Empty;
        public string Uom { get; set; } = "PCS";

        public decimal AvailableStock { get; set; }

        public decimal RetailPrice { get; set; }
        public decimal WholesalePrice { get; set; }
        public decimal MinimumPrice { get; set; }
        public decimal MaximumPrice { get; set; }

        public bool IsFreeItem { get; set; } = false;
        public string FreeReasonCode { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(GrossAmount))]
        [NotifyPropertyChangedFor(nameof(DiscountAmount))]
        [NotifyPropertyChangedFor(nameof(LineAmount))]
        private decimal _quantity = 1m;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(GrossAmount))]
        [NotifyPropertyChangedFor(nameof(DiscountAmount))]
        [NotifyPropertyChangedFor(nameof(LineAmount))]
        private decimal _unitPrice = 0m;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DiscountAmount))]
        [NotifyPropertyChangedFor(nameof(LineAmount))]
        private decimal _discountPercentage = 0m;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DiscountAmount))]
        [NotifyPropertyChangedFor(nameof(LineAmount))]
        private decimal _manualDiscountAmount = 0m;

        public decimal GrossAmount => Math.Round(Quantity * UnitPrice, 2);

        public decimal PercentageDiscountAmount =>
            Math.Round(GrossAmount * (DiscountPercentage / 100m), 2);

        public decimal DiscountAmount
        {
            get
            {
                decimal discount = PercentageDiscountAmount + ManualDiscountAmount;

                if (discount < 0)
                    return 0m;

                if (discount > GrossAmount)
                    return GrossAmount;

                return Math.Round(discount, 2);
            }
        }

        public decimal LineAmount
        {
            get
            {
                decimal amount = GrossAmount - DiscountAmount;
                return Math.Round(amount < 0 ? 0 : amount, 2);
            }
        }
    }
}