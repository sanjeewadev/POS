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

        // ==========================================
        // 1. REPORT PARAMETERS
        // ==========================================
        [ObservableProperty]
        private DateTime _startDate = DateTime.Today.AddDays(-30); // Default to last 30 days

        [ObservableProperty]
        private DateTime _endDate = DateTime.Today;

        // ==========================================
        // 2. MACRO KPI CARDS
        // ==========================================
        [ObservableProperty]
        private decimal _totalCompanyDebt = 0m;

        // ==========================================
        // 3. DTO COLLECTIONS (Bound to DataGrids)
        // ==========================================
        public ObservableCollection<AgedPayableDto> AgedPayables { get; set; } = new();
        public ObservableCollection<SupplierVolumeDto> PurchasingVolumes { get; set; } = new();
        public ObservableCollection<SupplierReturnRateDto> ReturnRates { get; set; } = new();

        public SupplierReportViewModel(SupplierReportRepository repository)
        {
            _repository = repository;

            // Auto-fire the report generation the second the manager opens the dashboard
            _ = GenerateReportAsync();
        }

        // ==========================================
        // 4. THE EXECUTION ENGINE
        // ==========================================
        [RelayCommand]
        private async Task GenerateReportAsync()
        {
            // Safety validation
            if (StartDate > EndDate)
            {
                MessageBox.Show("Start Date cannot be later than End Date.", "Invalid Date Range", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // 1. Fetch & Populate Aged Payables
                // (Notice we don't pass dates here, because aging is always relative to 'Today')
                var payablesData = await _repository.GetAgedPayablesSummaryAsync();
                AgedPayables.Clear();
                foreach (var item in payablesData) AgedPayables.Add(item);

                // Update the Master KPI
                TotalCompanyDebt = AgedPayables.Sum(p => p.TotalOwed);

                // 2. Fetch & Populate Purchasing Volume
                var volumeData = await _repository.GetPurchasingVolumeAsync(StartDate, EndDate);
                PurchasingVolumes.Clear();
                foreach (var item in volumeData) PurchasingVolumes.Add(item);

                // 3. Fetch & Populate Return Rates
                var returnData = await _repository.GetReturnRatesAsync(StartDate, EndDate);
                ReturnRates.Clear();
                foreach (var item in returnData) ReturnRates.Add(item);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Report generation failed: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==========================================
        // 5. EXPORT STUBS
        // ==========================================
        [RelayCommand]
        private void ExportToExcel()
        {
            if (!AgedPayables.Any() && !PurchasingVolumes.Any())
            {
                MessageBox.Show("No data available to export. Please generate the report first.", "Export Blocked", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MessageBox.Show("Excel Export module initializing...\n\n(This will be connected to the ClosedXML or EPPlus library in a future update).",
                            "Export to Excel", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void ExportToPdf()
        {
            if (!AgedPayables.Any() && !PurchasingVolumes.Any())
            {
                MessageBox.Show("No data available to export. Please generate the report first.", "Export Blocked", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MessageBox.Show("PDF Generation module initializing...\n\n(This will be connected to the iTextSharp or PDFsharp library in a future update).",
                            "Export to PDF", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}