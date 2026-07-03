using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class SupplierReportViewModel : ViewModelBase
    {
        private readonly SupplierReportRepository _repository;

        // =========================================================
        // REPORT PARAMETERS
        // =========================================================

        [ObservableProperty]
        private DateTime _startDate = DateTime.Today.AddDays(-30);

        [ObservableProperty]
        private DateTime _endDate = DateTime.Today;

        // =========================================================
        // KPI CARDS
        // =========================================================

        [ObservableProperty]
        private decimal _totalCompanyDebt = 0m;

        [ObservableProperty]
        private decimal _totalPurchaseValue = 0m;

        [ObservableProperty]
        private decimal _totalSupplierReturnValue = 0m;

        [ObservableProperty]
        private decimal _totalReturnedQty = 0m;

        [ObservableProperty]
        private int _supplierWithDebtCount = 0;

        [ObservableProperty]
        private int _supplierWithPurchaseCount = 0;

        [ObservableProperty]
        private int _supplierWithReturnCount = 0;

        // =========================================================
        // UI STATE
        // =========================================================

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        [ObservableProperty]
        private string _reportPeriodText = string.Empty;

        // =========================================================
        // REPORT COLLECTIONS
        // =========================================================

        public ObservableCollection<SupplierOutstandingSummaryDto> OutstandingSummaries { get; } = new();

        public ObservableCollection<SupplierPurchaseVolumeDto> PurchasingVolumes { get; } = new();

        public ObservableCollection<SupplierReturnSummaryDto> SupplierReturns { get; } = new();

        public SupplierReportViewModel(SupplierReportRepository repository)
        {
            _repository = repository;

            UpdateReportPeriodText();

            _ = GenerateReportAsync();
        }

        // =========================================================
        // DATE CHANGE EVENTS
        // =========================================================

        partial void OnStartDateChanged(DateTime value)
        {
            UpdateReportPeriodText();
        }

        partial void OnEndDateChanged(DateTime value)
        {
            UpdateReportPeriodText();
        }

        private void UpdateReportPeriodText()
        {
            ReportPeriodText = $"{StartDate:dd-MMM-yyyy} to {EndDate:dd-MMM-yyyy}";
        }

        // =========================================================
        // GENERATE REPORT
        // =========================================================

        [RelayCommand]
        private async Task GenerateReportAsync()
        {
            if (StartDate.Date > EndDate.Date)
            {
                MessageBox.Show(
                    "Start date cannot be later than end date.",
                    "Supplier Report",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            StatusMessage = "Generating supplier report...";

            try
            {
                OutstandingSummaries.Clear();
                PurchasingVolumes.Clear();
                SupplierReturns.Clear();

                var outstandingRows = await _repository.GetSupplierOutstandingSummaryAsync();

                foreach (var row in outstandingRows)
                    OutstandingSummaries.Add(row);

                var purchaseRows = await _repository.GetPurchasingVolumeAsync(
                    StartDate,
                    EndDate);

                foreach (var row in purchaseRows)
                    PurchasingVolumes.Add(row);

                var returnRows = await _repository.GetSupplierReturnSummaryAsync(
                    StartDate,
                    EndDate);

                foreach (var row in returnRows)
                    SupplierReturns.Add(row);

                CalculateKpis();

                StatusMessage =
                    $"Report generated. Outstanding: {OutstandingSummaries.Count}, Purchases: {PurchasingVolumes.Count}, Returns: {SupplierReturns.Count}.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Supplier report generation failed.";

                MessageBox.Show(
                    $"Supplier report generation failed:\n\n{ex.Message}",
                    "Supplier Report",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await GenerateReportAsync();
        }

        private void CalculateKpis()
        {
            TotalCompanyDebt = Math.Round(
                OutstandingSummaries
                    .Where(r => r.NetOutstanding > 0)
                    .Sum(r => r.NetOutstanding),
                2);

            SupplierWithDebtCount = OutstandingSummaries
                .Count(r => r.NetOutstanding > 0);

            TotalPurchaseValue = Math.Round(
                PurchasingVolumes.Sum(r => r.TotalGrnValue),
                2);

            SupplierWithPurchaseCount = PurchasingVolumes.Count;

            TotalSupplierReturnValue = Math.Round(
                SupplierReturns.Sum(r => r.NetSupplierCredit),
                2);

            TotalReturnedQty = Math.Round(
                SupplierReturns.Sum(r => r.TotalReturnedQty),
                3);

            SupplierWithReturnCount = SupplierReturns.Count;
        }

        // =========================================================
        // CLEAR / RESET
        // =========================================================

        [RelayCommand]
        private async Task ResetPeriodAsync()
        {
            StartDate = DateTime.Today.AddDays(-30);
            EndDate = DateTime.Today;

            await GenerateReportAsync();
        }

        // =========================================================
        // EXPORT PLACEHOLDERS
        // =========================================================

        [RelayCommand]
        private void ExportToExcel()
        {
            if (!HasReportData())
            {
                MessageBox.Show(
                    "No report data available to export. Please generate the report first.",
                    "Supplier Report",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            MessageBox.Show(
                "Excel export is not connected yet.\n\nThis report is ready for export later after the report layout is finalized.",
                "Supplier Report",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        [RelayCommand]
        private void ExportToPdf()
        {
            if (!HasReportData())
            {
                MessageBox.Show(
                    "No report data available to export. Please generate the report first.",
                    "Supplier Report",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            MessageBox.Show(
                "PDF export is not connected yet.\n\nThis report is ready for export later after the report layout is finalized.",
                "Supplier Report",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private bool HasReportData()
        {
            return OutstandingSummaries.Any() ||
                   PurchasingVolumes.Any() ||
                   SupplierReturns.Any();
        }
    }
}