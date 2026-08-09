using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.BackOffice.UI.Services;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using POS.Core.Services.Exports;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class ItemSalesAnalyticsViewModel : ViewModelBase
    {
        private readonly SalesAnalyticsRepository _repository;
        private readonly StoreSettingsRepository _storeSettingsRepository;
        private readonly OperationalExportBuilder _exportBuilder;
        private readonly ExportDialogService _exportDialog;
        private readonly ExportAuthorizationService _authorization;

        [ObservableProperty] private DateTime _startDate = DateTime.Today.AddDays(-30);
        [ObservableProperty] private DateTime _endDate = DateTime.Today;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _statusMessage = "Ready.";
        [ObservableProperty] private decimal _grossSales;
        [ObservableProperty] private decimal _discounts;
        [ObservableProperty] private decimal _returnValue;
        [ObservableProperty] private decimal _netSales;
        [ObservableProperty] private decimal _grossProfit;
        [ObservableProperty] private decimal _soldQuantity;
        [ObservableProperty] private decimal _returnedQuantity;
        [ObservableProperty] private int _sellingItemCount;
        [ObservableProperty] private int _slowOrNonSellingCount;
        [ObservableProperty] private ItemPerformanceDto? _selectedItem;

        public ObservableCollection<ItemPerformanceDto> Items { get; } = new();
        public ObservableCollection<ItemPerformanceDto> TopSellers { get; } = new();
        public ObservableCollection<ItemPerformanceDto> SlowOrNonSellingItems { get; } = new();
        public ObservableCollection<ItemSalesTransactionDto> Transactions { get; } = new();
        public bool IsEmpty => !IsBusy && Items.Count == 0;

        public ItemSalesAnalyticsViewModel(
            SalesAnalyticsRepository repository,
            StoreSettingsRepository storeSettingsRepository,
            OperationalExportBuilder exportBuilder,
            ExportDialogService exportDialog,
            ExportAuthorizationService authorization)
        {
            _repository = repository;
            _storeSettingsRepository = storeSettingsRepository;
            _exportBuilder = exportBuilder;
            _exportDialog = exportDialog;
            _authorization = authorization;
            _ = LoadAsync();
        }

        partial void OnSelectedItemChanged(ItemPerformanceDto? value) => _ = LoadTransactionsAsync(value?.ItemVariantId);
        partial void OnIsBusyChanged(bool value)
        {
            ExportPdfCommand.NotifyCanExecuteChanged();
            ExportCsvCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (StartDate.Date > EndDate.Date)
            {
                MessageBox.Show("Start date cannot be later than end date.", "Item Sales Analysis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            StatusMessage = "Calculating item sales...";
            try
            {
                ItemSalesAnalyticsResultDto result = await _repository.GetAnalyticsAsync(StartDate, EndDate, SearchText);
                Items.Clear();
                TopSellers.Clear();
                SlowOrNonSellingItems.Clear();

                foreach (ItemPerformanceDto row in result.Items)
                    Items.Add(row);
                foreach (ItemPerformanceDto row in result.Items.Where(row => row.NetQuantity > 0m).OrderByDescending(row => row.NetSales).Take(20))
                    TopSellers.Add(row);
                foreach (ItemPerformanceDto row in result.Items.Where(row => row.IsNonSelling).OrderBy(row => row.LastSaleDate).Take(50))
                    SlowOrNonSellingItems.Add(row);

                GrossSales = result.Summary.GrossSales;
                Discounts = result.Summary.Discounts;
                ReturnValue = result.Summary.ReturnValue;
                NetSales = result.Summary.NetSales;
                GrossProfit = result.Summary.GrossProfit;
                SoldQuantity = result.Summary.SoldQuantity;
                ReturnedQuantity = result.Summary.ReturnedQuantity;
                SellingItemCount = result.Summary.SellingItemCount;
                SlowOrNonSellingCount = result.Summary.NonSellingStockItemCount;
                SelectedItem = Items.Count > 0 ? Items[0] : null;
                StatusMessage = $"{Items.Count:N0} item/service row(s). Period activity basis.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Item sales analysis could not be loaded.";
                MessageBox.Show(ex.Message, "Item Sales Analysis", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(IsEmpty));
                ExportPdfCommand.NotifyCanExecuteChanged();
                ExportCsvCommand.NotifyCanExecuteChanged();
            }
        }

        [RelayCommand(CanExecute = nameof(CanExport))]
        private async Task ExportPdfAsync()
        {
            try
            {
                _authorization.EnsureBulkFinancialExportAllowed();
                StoreSettings settings = await _storeSettingsRepository.GetActiveAsync()
                    ?? StoreSettingsRepository.CreateDefaultSettings();
                PdfTableDocumentDto document = _exportBuilder.BuildItemSales(
                    Items.ToList(), StartDate, EndDate,
                    OperationalExportBuilder.StoreHeading(settings),
                    _authorization.CurrentUsername);
                _exportDialog.SaveTablePdf(
                    "Save Item Sales Analysis PDF",
                    ExportFileNameHelper.Build($"Item_Sales_{StartDate:yyyyMMdd}_{EndDate:yyyyMMdd}", ".pdf"),
                    document);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Item Sales PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        [RelayCommand(CanExecute = nameof(CanExport))]
        private async Task ExportCsvAsync()
        {
            try
            {
                _authorization.EnsureBulkFinancialExportAllowed();
                string csv = _exportBuilder.BuildItemSalesCsv(Items.ToList());
                await _exportDialog.SaveCsvAsync(
                    "Save Item Sales Analysis CSV",
                    ExportFileNameHelper.Build($"Item_Sales_{StartDate:yyyyMMdd}_{EndDate:yyyyMMdd}", ".csv"),
                    csv);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Item Sales CSV", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private bool CanExport() => !IsBusy && Items.Count > 0;

        private async Task LoadTransactionsAsync(int? itemVariantId)
        {
            Transactions.Clear();
            if (!itemVariantId.HasValue)
                return;

            try
            {
                var rows = await _repository.GetItemTransactionsAsync(itemVariantId.Value, StartDate, EndDate);
                foreach (ItemSalesTransactionDto row in rows)
                    Transactions.Add(row);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Item Transactions", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ResetAsync()
        {
            StartDate = DateTime.Today.AddDays(-30);
            EndDate = DateTime.Today;
            SearchText = string.Empty;
            await LoadAsync();
        }
    }
}
