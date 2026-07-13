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
    public partial class SupplierReportViewModel : ViewModelBase
    {
        private readonly SupplierReportRepository _repository;
        private readonly StoreSettingsRepository _storeSettingsRepository;
        private readonly OperationalExportBuilder _exportBuilder;
        private readonly ExportDialogService _exportDialog;
        private readonly ExportAuthorizationService _authorization;

        [ObservableProperty] private DateTime _startDate = DateTime.Today.AddDays(-30);
        [ObservableProperty] private DateTime _endDate = DateTime.Today;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _statusMessage = "Ready.";
        [ObservableProperty] private decimal _totalCompanyDebt;
        [ObservableProperty] private decimal _totalPurchaseValue;
        [ObservableProperty] private decimal _totalSupplierReturnValue;
        [ObservableProperty] private decimal _totalReturnedQty;
        [ObservableProperty] private int _supplierWithDebtCount;
        [ObservableProperty] private int _supplierWithPurchaseCount;
        [ObservableProperty] private int _supplierWithReturnCount;

        public ObservableCollection<SupplierOutstandingSummaryDto> OutstandingSummaries { get; } = new();
        public ObservableCollection<SupplierPurchaseVolumeDto> PurchasingVolumes { get; } = new();
        public ObservableCollection<SupplierReturnSummaryDto> SupplierReturns { get; } = new();
        public bool IsEmpty => !IsBusy && OutstandingSummaries.Count == 0 && PurchasingVolumes.Count == 0 && SupplierReturns.Count == 0;

        public SupplierReportViewModel(
            SupplierReportRepository repository,
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
                MessageBox.Show("Start date cannot be later than end date.", "Supplier Reports", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            StatusMessage = "Loading supplier reports...";
            try
            {
                var outstanding = await _repository.GetSupplierOutstandingSummaryAsync(SearchText);
                var purchases = await _repository.GetPurchasingVolumeAsync(StartDate, EndDate, SearchText);
                var returns = await _repository.GetSupplierReturnSummaryAsync(StartDate, EndDate, SearchText);

                OutstandingSummaries.Clear();
                PurchasingVolumes.Clear();
                SupplierReturns.Clear();
                foreach (SupplierOutstandingSummaryDto row in outstanding) OutstandingSummaries.Add(row);
                foreach (SupplierPurchaseVolumeDto row in purchases) PurchasingVolumes.Add(row);
                foreach (SupplierReturnSummaryDto row in returns) SupplierReturns.Add(row);

                TotalCompanyDebt = Math.Round(outstanding.Where(row => row.NetOutstanding > 0m).Sum(row => row.NetOutstanding), 2);
                TotalPurchaseValue = Math.Round(purchases.Sum(row => row.TotalGrnValue), 2);
                TotalSupplierReturnValue = Math.Round(returns.Sum(row => row.NetSupplierCredit), 2);
                TotalReturnedQty = Math.Round(returns.Sum(row => row.TotalReturnedQty), 3);
                SupplierWithDebtCount = outstanding.Count(row => row.NetOutstanding > 0m);
                SupplierWithPurchaseCount = purchases.Count;
                SupplierWithReturnCount = returns.Count;
                StatusMessage = $"Outstanding {outstanding.Count:N0}; purchasing {purchases.Count:N0}; returns {returns.Count:N0}.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Supplier reports could not be loaded.";
                MessageBox.Show(ex.Message, "Supplier Reports", MessageBoxButton.OK, MessageBoxImage.Error);
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
                PdfTableDocumentDto document = _exportBuilder.BuildSupplierSummary(
                    OutstandingSummaries.ToList(), PurchasingVolumes.ToList(), SupplierReturns.ToList(),
                    StartDate, EndDate, OperationalExportBuilder.StoreHeading(settings), _authorization.CurrentUsername);
                _exportDialog.SaveTablePdf(
                    "Save Supplier Summary PDF",
                    ExportFileNameHelper.Build($"Supplier_Summary_{StartDate:yyyyMMdd}_{EndDate:yyyyMMdd}", ".pdf"),
                    document);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Supplier Summary PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        [RelayCommand(CanExecute = nameof(CanExport))]
        private async Task ExportCsvAsync()
        {
            try
            {
                _authorization.EnsureBulkFinancialExportAllowed();
                string csv = _exportBuilder.BuildSupplierSummaryCsv(
                    OutstandingSummaries.ToList(), PurchasingVolumes.ToList(), SupplierReturns.ToList());
                await _exportDialog.SaveCsvAsync(
                    "Save Supplier Summary CSV",
                    ExportFileNameHelper.Build($"Supplier_Summary_{StartDate:yyyyMMdd}_{EndDate:yyyyMMdd}", ".csv"),
                    csv);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Supplier Summary CSV", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private bool CanExport() => !IsBusy && !IsEmpty;

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
