using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class StockBalanceViewModel : ObservableObject
    {
        private readonly StockBalanceRepository _stockRepository;
        private readonly CategoryRepository _categoryRepository;
        private readonly SupplierRepository _supplierRepository;

        private bool _suppressAutoRefresh = false;

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
        private bool _hideZeroStock = true;

        [ObservableProperty]
        private bool _showNegativeOnly = false;

        // =========================================================
        // DATA COLLECTIONS
        // =========================================================

        public ObservableCollection<Category> Categories { get; } = new();

        public ObservableCollection<Supplier> Suppliers { get; } = new();

        [ObservableProperty]
        private ObservableCollection<StockBalanceDto> _stockBalances = new();

        [ObservableProperty]
        private StockBalanceDto? _selectedItem;

        // =========================================================
        // FOOTER TOTALS
        // =========================================================

        [ObservableProperty]
        private int _totalLineItems = 0;

        [ObservableProperty]
        private int _totalBatchCount = 0;

        [ObservableProperty]
        private int _negativeLineCount = 0;

        [ObservableProperty]
        private int _zeroStockLineCount = 0;

        [ObservableProperty]
        private int _expiredBatchCount = 0;

        [ObservableProperty]
        private int _expiringSoonBatchCount = 0;

        [ObservableProperty]
        private decimal _totalPhysicalQty = 0m;

        [ObservableProperty]
        private decimal _totalAssetValue = 0m;

        [ObservableProperty]
        private decimal _projectedRevenue = 0m;

        [ObservableProperty]
        private decimal _projectedWholesaleValue = 0m;

        [ObservableProperty]
        private decimal _projectedGrossProfit = 0m;

        // =========================================================
        // UI STATE
        // =========================================================

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

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
            IsBusy = true;
            _suppressAutoRefresh = true;

            try
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

                foreach (var category in categories.OrderBy(c => c.CategoryName))
                    Categories.Add(category);

                Suppliers.Add(new Supplier
                {
                    Id = 0,
                    SupplierName = "-- ALL SUPPLIERS --",
                    CompanyName = "-- ALL SUPPLIERS --"
                });

                foreach (var supplier in suppliers.OrderBy(s => s.SupplierName))
                    Suppliers.Add(supplier);

                SelectedCategory = Categories.FirstOrDefault(c => c.Id == 0);
                SelectedSupplier = Suppliers.FirstOrDefault(s => s.Id == 0);
            }
            catch (Exception ex)
            {
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

        // =========================================================
        // LOAD DATA
        // =========================================================

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            IsBusy = true;

            try
            {
                int? categoryId = SelectedCategory != null && SelectedCategory.Id > 0
                    ? SelectedCategory.Id
                    : null;

                int? supplierId = SelectedSupplier != null && SelectedSupplier.Id > 0
                    ? SelectedSupplier.Id
                    : null;

                var data = await _stockRepository.GetStockBalancesAsync(
                    SearchText,
                    categoryId,
                    supplierId,
                    HideZeroStock,
                    ShowNegativeOnly);

                StockBalances = new ObservableCollection<StockBalanceDto>(data);

                CalculateGlobalTotals();

                StatusMessage = $"Loaded {TotalLineItems} item variant(s), {TotalBatchCount} active batch(es).";
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
                TotalBatchCount = 0;
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
            TotalBatchCount = StockBalances.Sum(x => x.BatchCount);

            NegativeLineCount = StockBalances.Count(x => x.TotalQtyOnHand < 0);
            ZeroStockLineCount = StockBalances.Count(x => x.TotalQtyOnHand == 0);
            ExpiredBatchCount = StockBalances.Count(x => x.HasExpiredBatch);
            ExpiringSoonBatchCount = StockBalances.Count(x => x.HasExpiringSoonBatch);

            TotalPhysicalQty = StockBalances.Sum(x => x.TotalQtyOnHand);
            TotalAssetValue = StockBalances.Sum(x => x.TotalCostValue);
            ProjectedRevenue = StockBalances.Sum(x => x.TotalRetailValue);
            ProjectedWholesaleValue = StockBalances.Sum(x => x.TotalWholesaleValue);
            ProjectedGrossProfit = ProjectedRevenue - TotalAssetValue;
        }

        // =========================================================
        // FILTER ACTIONS
        // =========================================================

        [RelayCommand]
        private void ClearFilters()
        {
            _suppressAutoRefresh = true;

            SearchText = string.Empty;
            SelectedCategory = Categories.FirstOrDefault(c => c.Id == 0);
            SelectedSupplier = Suppliers.FirstOrDefault(s => s.Id == 0);
            HideZeroStock = true;
            ShowNegativeOnly = false;

            _suppressAutoRefresh = false;

            _ = LoadDataAsync();
        }

        // =========================================================
        // EXPORT
        // Keep method name ExportExcel because existing XAML binds
        // to ExportExcelCommand.
        // =========================================================

        [RelayCommand]
        private void ExportExcel()
        {
            try
            {
                if (!StockBalances.Any())
                {
                    MessageBox.Show(
                        "There is no stock balance data to export.",
                        "Export",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                var dialog = new SaveFileDialog
                {
                    Title = "Export Stock Balance CSV",
                    Filter = "CSV File (*.csv)|*.csv",
                    FileName = $"Stock_Balance_{DateTime.Now:yyyyMMdd_HHmm}.csv"
                };

                if (dialog.ShowDialog() != true)
                    return;

                string csv = BuildCsvExport();

                File.WriteAllText(
                    dialog.FileName,
                    csv,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

                MessageBox.Show(
                    $"Stock balance exported successfully.\n\nRows: {TotalLineItems}\nBatches: {TotalBatchCount}",
                    "Export Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Export failed:\n\n{ex.Message}",
                    "Export Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private string BuildCsvExport()
        {
            var builder = new StringBuilder();

            builder.AppendLine(CsvRow(
                "Record Type",
                "Item Code",
                "SKU",
                "Barcode",
                "Description",
                "Variant",
                "UOM",
                "Category",
                "Supplier",
                "Stock Status",
                "Total Qty On Hand",
                "Unit Avg Cost",
                "Unit Retail",
                "Unit Wholesale",
                "Total Cost Value",
                "Total Retail Value",
                "Total Wholesale Value",
                "Potential Gross Profit",
                "Markup Percent",
                "Batch Count",
                "Earliest Expiry Date",
                "Last Received Date",
                "Batch No",
                "Batch Stock",
                "Batch Stock Status",
                "Batch Cost",
                "Batch Retail",
                "Batch Wholesale",
                "Batch Cost Value",
                "Batch Retail Value",
                "Batch Wholesale Value",
                "Batch Received Date",
                "Batch Expiry Date",
                "Batch Expiry Status",
                "Days To Expire"));

            foreach (var item in StockBalances)
            {
                if (item.Batches.Any())
                {
                    foreach (var batch in item.Batches)
                    {
                        builder.AppendLine(BuildCsvLine(item, batch));
                    }
                }
                else
                {
                    builder.AppendLine(BuildCsvLine(item, null));
                }
            }

            builder.AppendLine();
            builder.AppendLine(CsvRow("SUMMARY"));
            builder.AppendLine(CsvRow("Total Line Items", TotalLineItems.ToString(CultureInfo.InvariantCulture)));
            builder.AppendLine(CsvRow("Total Batches", TotalBatchCount.ToString(CultureInfo.InvariantCulture)));
            builder.AppendLine(CsvRow("Negative Lines", NegativeLineCount.ToString(CultureInfo.InvariantCulture)));
            builder.AppendLine(CsvRow("Zero Stock Lines", ZeroStockLineCount.ToString(CultureInfo.InvariantCulture)));
            builder.AppendLine(CsvRow("Expired Batch Lines", ExpiredBatchCount.ToString(CultureInfo.InvariantCulture)));
            builder.AppendLine(CsvRow("Expiring Soon Batch Lines", ExpiringSoonBatchCount.ToString(CultureInfo.InvariantCulture)));
            builder.AppendLine(CsvRow("Total Physical Qty", FormatQty(TotalPhysicalQty)));
            builder.AppendLine(CsvRow("Total Asset Value", FormatMoney(TotalAssetValue)));
            builder.AppendLine(CsvRow("Projected Retail Value", FormatMoney(ProjectedRevenue)));
            builder.AppendLine(CsvRow("Projected Wholesale Value", FormatMoney(ProjectedWholesaleValue)));
            builder.AppendLine(CsvRow("Projected Gross Profit", FormatMoney(ProjectedGrossProfit)));

            return builder.ToString();
        }

        private static string BuildCsvLine(
            StockBalanceDto item,
            ItemBatchDto? batch)
        {
            return CsvRow(
                batch == null ? "ITEM" : "BATCH",
                item.ItemCode,
                item.SkuCode,
                item.Barcode,
                item.Description,
                item.VariantDescription,
                item.Uom,
                item.CategoryName,
                item.PrimarySupplierName,
                item.StockStatus,
                FormatQty(item.TotalQtyOnHand),
                FormatMoney(item.UnitCost),
                FormatMoney(item.UnitRetail),
                FormatMoney(item.UnitWholesale),
                FormatMoney(item.TotalCostValue),
                FormatMoney(item.TotalRetailValue),
                FormatMoney(item.TotalWholesaleValue),
                FormatMoney(item.PotentialGrossProfit),
                FormatPercent(item.MarkupPercent),
                item.BatchCount.ToString(CultureInfo.InvariantCulture),
                FormatDate(item.EarliestExpiryDate),
                FormatDate(item.LastReceivedDate),
                batch?.BatchNo ?? string.Empty,
                batch == null ? string.Empty : FormatQty(batch.CurrentStock),
                batch?.StockStatus ?? string.Empty,
                batch == null ? string.Empty : FormatMoney(batch.CostPrice),
                batch == null ? string.Empty : FormatMoney(batch.RetailPrice),
                batch == null ? string.Empty : FormatMoney(batch.WholesalePrice),
                batch == null ? string.Empty : FormatMoney(batch.TotalBatchCost),
                batch == null ? string.Empty : FormatMoney(batch.TotalBatchRetail),
                batch == null ? string.Empty : FormatMoney(batch.TotalBatchWholesale),
                batch == null ? string.Empty : FormatDate(batch.ReceivedDate),
                batch == null ? string.Empty : FormatDate(batch.ExpiryDate),
                batch?.ExpiryStatus ?? string.Empty,
                batch?.DaysToExpire?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        }

        private static string CsvRow(params string[] values)
        {
            return string.Join(",", values.Select(Csv));
        }

        private static string Csv(string value)
        {
            value ??= string.Empty;

            value = value
                .Replace("\r\n", " ")
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();

            if (RequiresCsvProtection(value))
                value = "'" + value;

            if (value.Contains(',') || value.Contains('"') || value.Contains('\t'))
                return $"\"{value.Replace("\"", "\"\"")}\"";

            return value;
        }

        private static bool RequiresCsvProtection(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            char first = value[0];

            return first == '=' ||
                   first == '+' ||
                   first == '-' ||
                   first == '@';
        }

        private static string FormatQty(decimal value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string FormatMoney(decimal value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static string FormatPercent(decimal value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string FormatDate(DateTime? value)
        {
            return value.HasValue
                ? value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : string.Empty;
        }
    }
}