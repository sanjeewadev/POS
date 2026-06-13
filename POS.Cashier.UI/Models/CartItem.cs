using CommunityToolkit.Mvvm.ComponentModel;

namespace POS.Cashier.UI.Models
{
    // Inherits from ObservableObject so the DataGrid updates instantly when Qty changes
    public partial class CartItem : ObservableObject
    {
        public int ItemId { get; set; }
        public string Barcode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LineAmount))]
        private int _quantity = 1;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LineAmount))]
        private decimal _unitPrice;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LineAmount))]
        private decimal _discountPercentage = 0;

        // Auto-calculates the row total every time Qty, Price, or Discount changes
        public decimal LineAmount => (Quantity * UnitPrice) * (1 - (DiscountPercentage / 100m));
    }
}