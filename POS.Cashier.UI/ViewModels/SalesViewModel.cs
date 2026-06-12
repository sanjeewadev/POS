using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace POS.Cashier.UI.ViewModels
{
    // A simple wrapper class for the DataGrid rows
    public partial class CartItem : ObservableObject
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;

        [ObservableProperty]
        private int _quantity;

        [ObservableProperty]
        private decimal _unitPrice;

        public decimal TotalPrice => Quantity * UnitPrice;

        // Automatically update the TotalPrice if Qty or Price changes
        protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.PropertyName == nameof(Quantity) || e.PropertyName == nameof(UnitPrice))
            {
                OnPropertyChanged(nameof(TotalPrice));
            }
        }
    }

    public partial class SalesViewModel : ObservableObject
    {
        // The list that binds to your CartDataGrid
        public ObservableCollection<CartItem> CartItems { get; set; } = new();

        [ObservableProperty]
        private decimal _subtotal;

        [ObservableProperty]
        private decimal _taxAmount;

        [ObservableProperty]
        private decimal _grandTotal;

        [ObservableProperty]
        private string _barcodeInput = string.Empty;

        public SalesViewModel()
        {
            // Listen for changes in the cart to update totals
            CartItems.CollectionChanged += (s, e) => CalculateTotals();
        }

        // Simulates scanning a barcode or typing a code and hitting Enter
        [RelayCommand]
        private void AddItemToCart()
        {
            if (string.IsNullOrWhiteSpace(BarcodeInput)) return;

            // TODO: In the future, this will query POS.Core.Repositories.ItemRepository
            // For now, we just add a dummy item to test the UI!
            var newItem = new CartItem
            {
                ProductId = 1,
                ProductName = "Test Item (" + BarcodeInput + ")",
                Quantity = 1,
                UnitPrice = 15.00m
            };

            CartItems.Add(newItem);

            // Clear the search box instantly for the next scan
            BarcodeInput = string.Empty;
        }

        public void CalculateTotals()
        {
            Subtotal = CartItems.Sum(x => x.TotalPrice);
            TaxAmount = Subtotal * 0.08m; // Assuming 8% tax for example
            GrandTotal = Subtotal + TaxAmount;
        }

        [RelayCommand]
        private void ClearCart()
        {
            CartItems.Clear();
            CalculateTotals();
        }
    }
}