using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class StockBalanceViewModel : ObservableObject
    {
        private readonly StockBalanceRepository _stockRepository;
        private readonly CategoryRepository _categoryRepository;
        private readonly SupplierRepository _supplierRepository;

        // --- FILTERS ---
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private Category? _selectedCategory;
        [ObservableProperty] private Supplier? _selectedSupplier;
        [ObservableProperty] private bool _hideZeroStock = true; // Default to hiding clutter
        [ObservableProperty] private bool _showNegativeOnly = false;

        // --- DATA COLLECTIONS ---
        public ObservableCollection<Category> Categories { get; set; } = new();
        public ObservableCollection<Supplier> Suppliers { get; set; } = new();

        [ObservableProperty] private ObservableCollection<StockBalanceDto> _stockBalances = new();
        [ObservableProperty] private StockBalanceDto? _selectedItem;

        // --- GLOBAL FOOTER TOTALS ---
        [ObservableProperty] private int _totalLineItems = 0;
        [ObservableProperty] private decimal _totalPhysicalQty = 0m;
        [ObservableProperty] private decimal _totalAssetValue = 0m;
        [ObservableProperty] private decimal _projectedRevenue = 0m;

        public StockBalanceViewModel(
            StockBalanceRepository stockRepository,
            CategoryRepository categoryRepository,
            SupplierRepository supplierRepository)
        {
            _stockRepository = stockRepository;
            _categoryRepository = categoryRepository;
            _supplierRepository = supplierRepository;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            var categories = await _categoryRepository.GetAllAsync();
            var suppliers = await _supplierRepository.GetAllAsync();

            Categories.Add(new Category { Id = 0, CategoryName = "-- ALL CATEGORIES --" });
            foreach (var cat in categories) Categories.Add(cat);

            Suppliers.Add(new Supplier { Id = 0, CompanyName = "-- ALL SUPPLIERS --" });
            foreach (var sup in suppliers) Suppliers.Add(sup);

            await LoadDataAsync();
        }

        [RelayCommand]
        private async Task RefreshAsync() => await LoadDataAsync();

        private async Task LoadDataAsync()
        {
            try
            {
                int? catId = SelectedCategory?.Id != 0 ? SelectedCategory?.Id : null;
                int? supId = SelectedSupplier?.Id != 0 ? SelectedSupplier?.Id : null;

                // Load all data into a temporary variable first (Prevents UI Thread freezing)
                var rawData = await _stockRepository.GetStockBalancesAsync(
                    SearchText, catId, supId, HideZeroStock, ShowNegativeOnly);

                // Bulk update the collection
                StockBalances = new ObservableCollection<StockBalanceDto>(rawData);

                CalculateGlobalTotals();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load inventory valuation: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CalculateGlobalTotals()
        {
            if (!StockBalances.Any())
            {
                TotalLineItems = 0; TotalPhysicalQty = 0m; TotalAssetValue = 0m; ProjectedRevenue = 0m;
                return;
            }

            TotalLineItems = StockBalances.Count;
            TotalPhysicalQty = StockBalances.Sum(x => x.TotalQtyOnHand);
            TotalAssetValue = StockBalances.Sum(x => x.TotalCostValue);
            ProjectedRevenue = StockBalances.Sum(x => x.TotalRetailValue);
        }

        // --- AUTO-TRIGGERS ---
        partial void OnSelectedCategoryChanged(Category? value) => _ = LoadDataAsync();
        partial void OnSelectedSupplierChanged(Supplier? value) => _ = LoadDataAsync();

        partial void OnHideZeroStockChanged(bool value)
        {
            if (value && ShowNegativeOnly) ShowNegativeOnly = false;
            _ = LoadDataAsync();
        }

        partial void OnShowNegativeOnlyChanged(bool value)
        {
            if (value && HideZeroStock) HideZeroStock = false;
            _ = LoadDataAsync();
        }

        [RelayCommand]
        private void ClearFilters()
        {
            SearchText = string.Empty;
            SelectedCategory = Categories.FirstOrDefault(c => c.Id == 0);
            SelectedSupplier = Suppliers.FirstOrDefault(s => s.Id == 0);
            HideZeroStock = true;
            ShowNegativeOnly = false;
            _ = LoadDataAsync();
        }

        [RelayCommand]
        private void ExportExcel()
        {
            MessageBox.Show($"Exporting {TotalLineItems} rows with a Total Asset Value of Rs. {TotalAssetValue:N2} to Excel...", "Excel Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}