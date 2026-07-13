using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.BackOffice.UI.Services;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using POS.Core.Services.Exports;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class FinancialSummaryViewModel : ViewModelBase
    {
        private readonly FinancialAnalyticsRepository _repository;
        private readonly StoreSettingsRepository _storeSettingsRepository;
        private readonly OperationalExportBuilder _exportBuilder;
        private readonly ExportDialogService _exportDialog;
        private readonly ExportAuthorizationService _authorization;
        private FinancialSummaryDto? _currentSummary;

        [ObservableProperty] private DateTime _startDate = DateTime.Today.AddDays(-30);
        [ObservableProperty] private DateTime _endDate = DateTime.Today;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _statusMessage = "Ready.";
        [ObservableProperty] private decimal _grossMerchandiseSales;
        [ObservableProperty] private decimal _totalDiscounts;
        [ObservableProperty] private decimal _merchandiseSalesAfterDiscounts;
        [ObservableProperty] private decimal _customerReturns;
        [ObservableProperty] private decimal _netSales;
        [ObservableProperty] private decimal _saleCostOfGoods;
        [ObservableProperty] private decimal _returnedCostOfGoods;
        [ObservableProperty] private decimal _netCostOfGoods;
        [ObservableProperty] private decimal _grossProfit;
        [ObservableProperty] private decimal _giftVoucherIssueValue;
        [ObservableProperty] private decimal _postedPurchases;
        [ObservableProperty] private decimal _postedSupplierReturns;
        [ObservableProperty] private decimal _paidIn;
        [ObservableProperty] private decimal _paidOut;
        [ObservableProperty] private decimal _floatIn;
        [ObservableProperty] private decimal _floatOut;
        [ObservableProperty] private decimal _customerCashRefunds;
        [ObservableProperty] private int _totalSalesCount;
        [ObservableProperty] private decimal _averageSaleValue;

        public ObservableCollection<FinancialTenderTotalDto> TenderTotals { get; } = new();

        public FinancialSummaryViewModel(
            FinancialAnalyticsRepository repository,
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

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (StartDate.Date > EndDate.Date)
            {
                MessageBox.Show("Start date cannot be later than end date.", "Financial Summary", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            StatusMessage = "Calculating operational financial totals...";
            try
            {
                FinancialSummaryDto result = await _repository.GetFinancialSummaryAsync(StartDate, EndDate);
                _currentSummary = result;
                GrossMerchandiseSales = result.GrossMerchandiseSales;
                TotalDiscounts = result.TotalDiscounts;
                MerchandiseSalesAfterDiscounts = result.MerchandiseSalesAfterDiscounts;
                CustomerReturns = result.CustomerReturns;
                NetSales = result.NetSales;
                SaleCostOfGoods = result.SaleCostOfGoods;
                ReturnedCostOfGoods = result.ReturnedCostOfGoods;
                NetCostOfGoods = result.NetCostOfGoods;
                GrossProfit = result.GrossProfit;
                GiftVoucherIssueValue = result.GiftVoucherIssueValue;
                PostedPurchases = result.PostedPurchases;
                PostedSupplierReturns = result.PostedSupplierReturns;
                PaidIn = result.PaidIn;
                PaidOut = result.PaidOut;
                FloatIn = result.FloatIn;
                FloatOut = result.FloatOut;
                CustomerCashRefunds = result.CustomerCashRefunds;
                TotalSalesCount = result.TotalSalesCount;
                AverageSaleValue = result.AverageSaleValue;

                TenderTotals.Clear();
                foreach (FinancialTenderTotalDto row in result.TenderTotals)
                    TenderTotals.Add(row);

                StatusMessage = "Operational summary loaded. This is not a full accounting profit and loss statement.";
            }
            catch (Exception ex)
            {
                _currentSummary = null;
                StatusMessage = "Financial Summary could not be loaded.";
                MessageBox.Show(ex.Message, "Financial Summary", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                ExportPdfCommand.NotifyCanExecuteChanged();
            }
        }

        [RelayCommand(CanExecute = nameof(CanExport))]
        private async Task ExportPdfAsync()
        {
            try
            {
                _authorization.EnsureBulkFinancialExportAllowed();
                FinancialSummaryDto summary = _currentSummary
                    ?? throw new InvalidOperationException("Load the Financial Summary before exporting.");
                StoreSettings settings = await _storeSettingsRepository.GetActiveAsync()
                    ?? StoreSettingsRepository.CreateDefaultSettings();
                PdfTableDocumentDto document = _exportBuilder.BuildFinancialSummary(
                    summary,
                    StartDate,
                    EndDate,
                    OperationalExportBuilder.StoreHeading(settings),
                    _authorization.CurrentUsername);
                _exportDialog.SaveTablePdf(
                    "Save Financial Summary PDF",
                    ExportFileNameHelper.Build($"Financial_Summary_{StartDate:yyyyMMdd}_{EndDate:yyyyMMdd}", ".pdf"),
                    document);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Financial Summary Export", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private bool CanExport() => !IsBusy && _currentSummary != null;

        partial void OnIsBusyChanged(bool value) => ExportPdfCommand.NotifyCanExecuteChanged();

        [RelayCommand]
        private async Task ResetAsync()
        {
            StartDate = DateTime.Today.AddDays(-30);
            EndDate = DateTime.Today;
            await LoadAsync();
        }
    }
}
