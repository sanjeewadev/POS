using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using POS.Cashier.UI.ViewModels;
using POS.Core.Repositories; // This brings in your new ProductSeekDto

namespace POS.Cashier.UI.Views.Dialogs
{
    public partial class ProductSeekDialog : Window
    {
        private readonly ItemMasterRepository _itemRepo;
        private readonly SalesViewModel _viewModel;

        // Now using the highly optimized DTO directly from your repository
        public ObservableCollection<ProductSeekDto> SearchResults { get; set; } = new();

        public ProductSeekDialog(SalesViewModel viewModel, ItemMasterRepository itemRepo)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _itemRepo = itemRepo;

            ResultsGrid.ItemsSource = SearchResults;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Note: If you have a CategoryRepository, you can load these dynamically later.
            // For now, we leave the UI static categories to ensure the search works instantly.
            CategoryCombo.Items.Add("ALL CATEGORIES");
            CategoryCombo.Items.Add("Beverages");
            CategoryCombo.Items.Add("Snacks");
            CategoryCombo.Items.Add("Groceries");
            CategoryCombo.Items.Add("Cleaning");
            CategoryCombo.SelectedIndex = 0;

        }

        // ==========================================
        // REAL-TIME SEARCH ENGINE
        // ==========================================

        private async void SearchInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            await PerformSearchAsync();
        }

        private async void CategoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoryCombo.IsLoaded)
            {
                await PerformSearchAsync();
            }
        }

        private async Task PerformSearchAsync()
        {
            string nameFilter = SearchNameTxt.Text.Trim();
            string barcodeFilter = SearchBarcodeTxt.Text.Trim();
            string categoryFilter = CategoryCombo.SelectedItem?.ToString() ?? "ALL CATEGORIES";

            try
            {
                SearchResults.Clear();

                // >>> THE REAL DATABASE CALL <<<
                var results = await _itemRepo.SearchSeekItemsAsync(nameFilter, barcodeFilter, categoryFilter);

                foreach (var item in results)
                {
                    SearchResults.Add(item);
                }

                StatusTxt.Text = $"Found {SearchResults.Count} items.";
                StatusTxt.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.ColorConverter.ConvertFromString("#555555") != null ? (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#555555")! : System.Windows.Media.Colors.Gray);
            }
            catch (Exception ex)
            {
                StatusTxt.Text = "Search failed.";
                MessageBox.Show($"Search Error: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==========================================
        // UI INTERACTION & LIVE ADDING
        // ==========================================

        private void ResultsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = ResultsGrid.SelectedItem as ProductSeekDto;

            if (selectedItem != null)
            {
                ActionPanel.IsEnabled = true;

                if (selectedItem.StockOnHand <= 0)
                {
                    StatusTxt.Text = "⚠️ WARNING: This item is currently OUT OF STOCK.";
                    StatusTxt.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DC3545")!); // Red
                }
                else
                {
                    StatusTxt.Text = "Item selected. Click ADD ITEM.";
                    StatusTxt.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#005500")!); // Green
                }
            }
            else
            {
                ActionPanel.IsEnabled = false;
            }
        }

        private async void AddToCartBtn_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = ResultsGrid.SelectedItem as ProductSeekDto;

            if (selectedItem != null)
            {
                // Push the item to the cart and capture the silent true/false result
                bool isSuccess = await _viewModel.AddSpecificItemToCartAsync(selectedItem.Barcode, 1);

                if (isSuccess)
                {
                    // Show Green Success Message
                    StatusTxt.Text = $"✅ Added {selectedItem.Description} to Cart!";
                    StatusTxt.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#005500")!);
                }
                else
                {
                    // Show Red Error Message right inside the panel
                    StatusTxt.Text = $"❌ Failed to add item. Check shift status.";
                    StatusTxt.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DC3545")!);
                }
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}