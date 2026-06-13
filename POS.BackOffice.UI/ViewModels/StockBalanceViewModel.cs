using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class StockBalanceViewModel : ViewModelBase
    {
        private readonly StockBalanceRepository _stockRepository;
        private readonly CategoryRepository _categoryRepository;
        private readonly SupplierRepository _supplierRepository;

        // --- FILTERS ---
        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private Category? _selectedCategory;

        [ObservableProperty]
        private Supplier? _selectedSupplier;

        [ObservableProperty]
        private bool _hideZeroStock = false;

        [ObservableProperty]
        private bool _showNegativeOnly = false;

        // --- DATA COLLECTIONS ---
        public ObservableCollection<Category> Categories { get; set; } = new();
        public ObservableCollection<Supplier> Suppliers { get; set; } = new();
        public ObservableCollection<StockBalanceDto> StockBalances { get; set; } = new();

        [ObservableProperty]
        private StockBalanceDto? _selectedItem;

        // --- GLOBAL FOOTER TOTALS ---
        [ObservableProperty]
        private int _totalLineItems = 0;

        [ObservableProperty]
        private decimal _totalPhysicalQty = 0m;

        [ObservableProperty]
        private decimal _totalAssetValue = 0m;

        [ObservableProperty]
        private decimal _projectedRevenue = 0m; // Binds to Total Retail Value in UI

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
            // 1. Load Filter Dropdowns
            var categories = await _categoryRepository.GetAllAsync();
            var suppliers = await _supplierRepository.GetAllAsync();

            // Add an "All" option to the top of the lists
            Categories.Add(new Category { Id = 0, CategoryName = "-- ALL CATEGORIES --" });
            foreach (var cat in categories) Categories.Add(cat);

            Suppliers.Add(new Supplier { Id = 0, CompanyName = "-- ALL SUPPLIERS --" });
            foreach (var sup in suppliers) Suppliers.Add(sup);

            // 2. Load Initial Data
            await LoadDataAsync();
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                StockBalances.Clear();

                int? catId = SelectedCategory != null && SelectedCategory.Id != 0 ? SelectedCategory.Id : null;
                int? supId = SelectedSupplier != null && SelectedSupplier.Id != 0 ? SelectedSupplier.Id : null;

                // Call the high-speed repository pipeline
                var data = await _stockRepository.GetStockBalancesAsync(
                    SearchText,
                    catId,
                    supId,
                    HideZeroStock,
                    ShowNegativeOnly);

                foreach (var item in data)
                {
                    StockBalances.Add(item);
                }

                // Update the massive footer totals instantly
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
                TotalLineItems = 0;
                TotalPhysicalQty = 0m;
                TotalAssetValue = 0m;
                ProjectedRevenue = 0m;
                return;
            }

            // Since the SQL server already did the heavy multiplication (Qty * Cost) for each line,
            // we just do a lightning-fast memory summation here.
            TotalLineItems = StockBalances.Count;
            TotalPhysicalQty = StockBalances.Sum(x => x.QtyOnHand);
            TotalAssetValue = StockBalances.Sum(x => x.TotalCostValue);
            ProjectedRevenue = StockBalances.Sum(x => x.TotalRetailValue);
        }

        // --- AUTO-TRIGGERS (Instant UI Feedback) ---

        // When a user selects a Category, automatically refresh the grid
        partial void OnSelectedCategoryChanged(Category? value) => _ = LoadDataAsync();

        // When a user selects a Supplier, automatically refresh the grid
        partial void OnSelectedSupplierChanged(Supplier? value) => _ = LoadDataAsync();

        // When user toggles zero stock, refresh
        partial void OnHideZeroStockChanged(bool value)
        {
            if (value && ShowNegativeOnly) ShowNegativeOnly = false; // Mutually exclusive logic
            _ = LoadDataAsync();
        }

        // When user toggles negative stock, refresh
        partial void OnShowNegativeOnlyChanged(bool value)
        {
            if (value && HideZeroStock) HideZeroStock = false; // Mutually exclusive logic
            _ = LoadDataAsync();
        }

        [RelayCommand]
        private void ClearFilters()
        {
            SearchText = string.Empty;
            SelectedCategory = Categories.FirstOrDefault(c => c.Id == 0);
            SelectedSupplier = Suppliers.FirstOrDefault(s => s.Id == 0);
            HideZeroStock = false;
            ShowNegativeOnly = false;

            _ = LoadDataAsync();
        }

        // --- EXPORT & REPORTING ---

        [RelayCommand]
        private void PrintReport()
        {
            MessageBox.Show("This will send the current filtered view to the Crystal Reports / PDF engine.", "Print Triggered", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void ExportExcel()
        {
            // In a production environment, you would use a library like ClosedXML or EPPlus here.
            // Example:
            // using var workbook = new XLWorkbook();
            // var worksheet = workbook.Worksheets.Add("Inventory Valuation");
            // worksheet.Cell(1, 1).InsertTable(StockBalances);
            // workbook.SaveAs("ValuationReport.xlsx");

            MessageBox.Show($"Exporting {TotalLineItems} rows with a Total Asset Value of Rs. {TotalAssetValue:N2} to Excel...", "Excel Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}