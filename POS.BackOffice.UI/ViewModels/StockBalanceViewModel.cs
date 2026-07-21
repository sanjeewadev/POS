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
using POS.Core.Services.Exports;
using POS.BackOffice.UI.Services;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class StockBalanceViewModel : ObservableObject
    {
        private readonly StockBalanceRepository _stockRepository;
        private readonly CategoryRepository _categoryRepository;
        private readonly SupplierRepository _supplierRepository;
        private readonly OperationalExportBuilder _exportBuilder;
        private readonly ExportDialogService _exportDialog;
        private readonly ExportAuthorizationService _authorization;

        private bool _suppressAutoRefresh;

        // =========================================================
        // FILTERS
        // =========================================================

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

        public ObservableCollection<string> InventoryViews { get; } = new()
        {
            "Stock Balance & Valuation",
            "Expiry Monitor"
        };

        [ObservableProperty]
        private string _selectedInventoryView = "Stock Balance & Valuation";

        public ObservableCollection<string> ExpiryFilters { get; } =
            new(ExpiryMonitorFilters.Values);

        [ObservableProperty]
        private string _selectedExpiryFilter = ExpiryMonitorFilters.All;

        [ObservableProperty]
        private bool _positiveExpiryStockOnly = true;

        // =========================================================
        // DATA COLLECTIONS
        // =========================================================

        public ObservableCollection<Category> Categories { get; } = new();

        public ObservableCollection<Supplier> Suppliers { get; } = new();

        [ObservableProperty]
        private ObservableCollection<StockBalanceDto> _stockBalances = new();

        [ObservableProperty]
        private StockBalanceDto? _selectedItem;

        [ObservableProperty]
        private ObservableCollection<ExpiryMonitorRowDto> _expiryRows = new();

        [ObservableProperty]
        private ExpiryMonitorRowDto? _selectedExpiryRow;

        // =========================================================
        // FOOTER TOTALS
        // =========================================================

        [ObservableProperty]
        private int _totalLineItems;

        [ObservableProperty]
        private int _averageCostLineCount;

        [ObservableProperty]
        private int _batchTrackedLineCount;

        [ObservableProperty]
        private int _batchExpiryLineCount;

        [ObservableProperty]
        private int _totalBatchCount;

        [ObservableProperty]
        private int _totalStockBucketCount;

        [ObservableProperty]
        private int _negativeLineCount;

        [ObservableProperty]
        private int _zeroStockLineCount;

        [ObservableProperty]
        private int _expiredBatchCount;

        [ObservableProperty]
        private int _expiringSoonBatchCount;

        [ObservableProperty]
        private decimal _totalPhysicalQty;

        [ObservableProperty]
        private decimal _totalAssetValue;

        [ObservableProperty]
        private decimal _projectedRevenue;

        [ObservableProperty]
        private decimal _projectedWholesaleValue;

        [ObservableProperty]
        private decimal _projectedGrossProfit;

        [ObservableProperty]
        private int _expiryRowCount;

        [ObservableProperty]
        private int _expiredRowCount;

        [ObservableProperty]
        private int _expiresWithin7DaysCount;

        [ObservableProperty]
        private int _expiresWithin30DaysCount;

        [ObservableProperty]
        private int _expiresWithin90DaysCount;

        [ObservableProperty]
        private int _missingExpiryCount;

        [ObservableProperty]
        private decimal _expiryQuantityTotal;

        [ObservableProperty]
        private decimal _expiryCostValueTotal;

        // =========================================================
        // UI STATE
        // =========================================================

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        public StockBalanceViewModel(
            StockBalanceRepository stockRepository,
            CategoryRepository categoryRepository,
            SupplierRepository supplierRepository,
            OperationalExportBuilder exportBuilder,
            ExportDialogService exportDialog,
            ExportAuthorizationService authorization)
        {
            _stockRepository = stockRepository ?? throw new ArgumentNullException(nameof(stockRepository));
            _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
            _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
            _exportBuilder = exportBuilder ?? throw new ArgumentNullException(nameof(exportBuilder));
            _exportDialog = exportDialog ?? throw new ArgumentNullException(nameof(exportDialog));
            _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            IsBusy = true;
            _suppressAutoRefresh = true;

            try
            {
                await LoadLookupsAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to initialize Stock Balance page.";

                MessageBox.Show(
                    $"Failed to initialize Stock Balance page:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _suppressAutoRefresh = false;
                IsBusy = false;
            }

            await LoadDataAsync();
        }

        private async Task LoadLookupsAsync()
        {
            Categories.Clear();
            Suppliers.Clear();

            var categories = await _categoryRepository.GetAllAsync();
            var suppliers = await _supplierRepository.GetAllAsync();

            Categories.Add(new Category
            {
                Id = 0,
                CategoryName = "-- ALL CATEGORIES --"
            });

            foreach (var category in categories
                         .Where(c => !c.IsDeactivated)
                         .OrderBy(c => c.CategoryName))
            {
                Categories.Add(category);
            }

            Suppliers.Add(new Supplier
            {
                Id = 0,
                SupplierName = "-- ALL SUPPLIERS --",
                CompanyName = "-- ALL SUPPLIERS --"
            });

            foreach (var supplier in suppliers
                         .Where(s => !s.IsDeactivated)
                         .OrderBy(s => s.SupplierName))
            {
                Suppliers.Add(supplier);
            }

            SelectedCategory = Categories.FirstOrDefault(c => c.Id == 0);
            SelectedSupplier = Suppliers.FirstOrDefault(s => s.Id == 0);
        }

        // =========================================================
        // FILTER EVENTS
        // =========================================================

        partial void OnSelectedCategoryChanged(Category? value)
        {
            if (!_suppressAutoRefresh)
                _ = LoadDataAsync();
        }

        partial void OnSelectedSupplierChanged(Supplier? value)
        {
            if (!_suppressAutoRefresh)
                _ = LoadDataAsync();
        }

        partial void OnSelectedExpiryFilterChanged(string value)
        {
            if (!_suppressAutoRefresh)
                _ = LoadDataAsync();
        }

        partial void OnPositiveExpiryStockOnlyChanged(bool value)
        {
            if (!_suppressAutoRefresh)
                _ = LoadDataAsync();
        }

        partial void OnHideZeroStockChanged(bool value)
        {
            if (_suppressAutoRefresh)
                return;

            if (value && ShowNegativeOnly)
            {
                _suppressAutoRefresh = true;
                ShowNegativeOnly = false;
                _suppressAutoRefresh = false;
            }

            _ = LoadDataAsync();
        }

        partial void OnShowNegativeOnlyChanged(bool value)
        {
            if (_suppressAutoRefresh)
                return;

            if (value && HideZeroStock)
            {
                _suppressAutoRefresh = true;
                HideZeroStock = false;
                _suppressAutoRefresh = false;
            }

            _ = LoadDataAsync();
        }

        partial void OnIsBusyChanged(bool value)
        {
            RefreshCommand.NotifyCanExecuteChanged();
            ClearFiltersCommand.NotifyCanExecuteChanged();
            ExportCsvCommand.NotifyCanExecuteChanged();
            ExportExpiryCsvCommand.NotifyCanExecuteChanged();
        }

        // =========================================================
        // LOAD DATA
        // =========================================================

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task RefreshAsync()
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            if (IsBusy)
                return;

            IsBusy = true;

            try
            {
                int? categoryId = SelectedCategory != null && SelectedCategory.Id > 0
                    ? SelectedCategory.Id
                    : null;

                int? supplierId = SelectedSupplier != null && SelectedSupplier.Id > 0
                    ? SelectedSupplier.Id
                    : null;

                var stockTask = _stockRepository.GetStockBalancesAsync(
                    SearchText,
                    categoryId,
                    supplierId,
                    HideZeroStock,
                    ShowNegativeOnly);

                var expiryTask = _stockRepository.GetExpiryMonitorAsync(
                    SearchText,
                    categoryId,
                    supplierId,
                    SelectedExpiryFilter,
                    PositiveExpiryStockOnly);

                await Task.WhenAll(stockTask, expiryTask);

                StockBalances = new ObservableCollection<StockBalanceDto>(
                    await stockTask);

                ExpiryRows = new ObservableCollection<ExpiryMonitorRowDto>(
                    await expiryTask);

                CalculateGlobalTotals();
                CalculateExpiryTotals();

                StatusMessage =
                    $"Loaded {TotalLineItems} stock variant(s) and {ExpiryRowCount} expiry-monitor row(s). " +
                    $"Average-cost: {AverageCostLineCount}, Batch-tracked: {BatchTrackedLineCount}, " +
                    $"Expired rows: {ExpiredRowCount}.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load stock balance.";

                MessageBox.Show(
                    $"Failed to load inventory valuation:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void CalculateGlobalTotals()
        {
            if (!StockBalances.Any())
            {
                TotalLineItems = 0;
                AverageCostLineCount = 0;
                BatchTrackedLineCount = 0;
                BatchExpiryLineCount = 0;
                TotalBatchCount = 0;
                TotalStockBucketCount = 0;
                NegativeLineCount = 0;
                ZeroStockLineCount = 0;
                ExpiredBatchCount = 0;
                ExpiringSoonBatchCount = 0;

                TotalPhysicalQty = 0m;
                TotalAssetValue = 0m;
                ProjectedRevenue = 0m;
                ProjectedWholesaleValue = 0m;
                ProjectedGrossProfit = 0m;
                return;
            }

            TotalLineItems = StockBalances.Count;
            AverageCostLineCount = StockBalances.Count(x => !x.HasBatchTracking);
            BatchTrackedLineCount = StockBalances.Count(x => x.HasBatchTracking);
            BatchExpiryLineCount = StockBalances.Count(x => x.HasBatchTracking && x.HasExpiryTracking);

            TotalBatchCount = StockBalances.Sum(x => x.BatchCount);
            TotalStockBucketCount = StockBalances.Sum(x => x.StockBucketCount);

            NegativeLineCount = StockBalances.Count(x => x.TotalQtyOnHand < 0m);
            ZeroStockLineCount = StockBalances.Count(x => x.TotalQtyOnHand == 0m);
            ExpiredBatchCount = StockBalances.Count(x => x.HasExpiredBatch);
            ExpiringSoonBatchCount = StockBalances.Count(x => x.HasExpiringSoonBatch);

            TotalPhysicalQty = StockBalances.Sum(x => x.TotalQtyOnHand);
            TotalAssetValue = StockBalances.Sum(x => x.TotalCostValue);
            ProjectedRevenue = StockBalances.Sum(x => x.TotalRetailValue);
            ProjectedWholesaleValue = StockBalances.Sum(x => x.TotalWholesaleValue);
            ProjectedGrossProfit = ProjectedRevenue - TotalAssetValue;
        }

        private void CalculateExpiryTotals()
        {
            ExpiryRowCount = ExpiryRows.Count;
            ExpiredRowCount = ExpiryRows.Count(row => row.IsExpired);
            ExpiresWithin7DaysCount = ExpiryRows.Count(row => row.IsExpiringWithin(7));
            ExpiresWithin30DaysCount = ExpiryRows.Count(row => row.IsExpiringWithin(30));
            ExpiresWithin90DaysCount = ExpiryRows.Count(row => row.IsExpiringWithin(90));
            MissingExpiryCount = ExpiryRows.Count(row => row.IsMissingExpiry);
            ExpiryQuantityTotal = ExpiryRows.Sum(row => row.AvailableQty);
            ExpiryCostValueTotal = ExpiryRows.Sum(row => row.CostValue);
        }

        // =========================================================
        // FILTER ACTIONS
        // =========================================================

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private void ClearFilters()
        {
            _suppressAutoRefresh = true;

            SearchText = string.Empty;
            SelectedCategory = Categories.FirstOrDefault(c => c.Id == 0);
            SelectedSupplier = Suppliers.FirstOrDefault(s => s.Id == 0);
            HideZeroStock = false;
            ShowNegativeOnly = false;
            SelectedExpiryFilter = ExpiryMonitorFilters.All;
            PositiveExpiryStockOnly = true;

            _suppressAutoRefresh = false;

            _ = LoadDataAsync();
        }

        // =========================================================
        // EXPORT
        // =========================================================

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task ExportCsvAsync()
        {
            try
            {
                _authorization.EnsureBulkFinancialExportAllowed();
                string csv = _exportBuilder.BuildStockBalanceCsv(StockBalances.ToList());
                await _exportDialog.SaveCsvAsync(
                    "Save Stock Balance CSV",
                    ExportFileNameHelper.Build($"Stock_Balance_{DateTime.Now:yyyyMMdd_HHmm}", ".csv"),
                    csv);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Stock Balance CSV", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task ExportExpiryCsvAsync()
        {
            try
            {
                _authorization.EnsureBulkFinancialExportAllowed();
                string csv = _exportBuilder.BuildExpiryMonitorCsv(
                    ExpiryRows.ToList());

                await _exportDialog.SaveCsvAsync(
                    "Save Expiry Monitor CSV",
                    ExportFileNameHelper.Build(
                        $"Expiry_Monitor_{DateTime.Now:yyyyMMdd_HHmm}",
                        ".csv"),
                    csv);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Expiry Monitor CSV",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private bool CanRunCommand()
        {
            return !IsBusy;
        }
    }
}
