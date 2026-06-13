using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Cashier.UI.Models;
using POS.Core.Repositories; // Your existing Back Office repositories!

namespace POS.Cashier.UI.ViewModels
{
    public partial class SalesViewModel : ObservableObject
    {
        private readonly ItemMasterRepository _itemRepository;

        // --- THE CART ---
        public ObservableCollection<CartItem> Cart { get; } = new();

        // --- THE TOTALS PANEL BINDINGS ---
        [ObservableProperty] private decimal _netValue = 0.00m;
        [ObservableProperty] private decimal _totalDiscount = 0.00m;
        [ObservableProperty] private int _totalItems = 0;
        [ObservableProperty] private int _totalPieces = 0;

        public SalesViewModel(ItemMasterRepository itemRepository)
        {
            _itemRepository = itemRepository;

            // Listen to the cart: Whenever an item is added or removed, recalculate the massive red numbers
            Cart.CollectionChanged += (s, e) => RecalculateTotals();
        }

        // ==========================================
        // THE GHOST SCANNER RECEIVER
        // ==========================================
        public async Task ProcessBarcodeAsync(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return;

            // 1. Check if the item is already in the cart
            var existingItem = Cart.FirstOrDefault(c => c.Barcode == barcode);

            if (existingItem != null)
            {
                // Just bump the quantity and the UI will auto-update!
                existingItem.Quantity++;
                RecalculateTotals();
                return;
            }

            // 2. It's a new item! Ask your existing Back Office database for it.
            try
            {
                // NOTE: Make sure your ItemRepository has a method like GetItemByBarcodeAsync!
                var dbItem = await _itemRepository.GetItemByBarcodeAsync(barcode);

                if (dbItem == null)
                {
                    MessageBox.Show($"Unrecognized Barcode: {barcode}", "Item Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (dbItem.TotalStockOnHand <= 0)
                {
                    // Optional warning: You can allow negative sales, or block them based on your GRN logic
                    MessageBox.Show("Warning: This item has 0 stock in the system.", "Low Stock", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                // 3. Add it to the Cashier's Cart
                // 3. Add it to the Cashier's Cart
                Cart.Add(new CartItem
                {
                    ItemId = dbItem.Id,
                    Barcode = dbItem.SkuCode, // The scanned code

                    // Pull the name from the Parent matrix
                    Description = dbItem.ItemParent?.ItemName ?? "Unknown Item",

                    UnitPrice = dbItem.RetailPrice, // Assuming your Variant model has RetailPrice
                    Quantity = 1
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Error: {ex.Message}", "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==========================================
        // MATH ENGINE
        // ==========================================
        public void RecalculateTotals()
        {
            NetValue = Cart.Sum(c => c.LineAmount);
            TotalItems = Cart.Count;
            TotalPieces = Cart.Sum(c => c.Quantity);

            // If you add discount logic later, calculate the difference here
            TotalDiscount = Cart.Sum(c => (c.UnitPrice * c.Quantity) - c.LineAmount);
        }

        [RelayCommand]
        private void ClearCart()
        {
            Cart.Clear();
        }
    }
}