using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.BackOffice.UI.Services;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using POS.Core.Services.Exports;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class VatReportViewModel : ViewModelBase
    {
        private readonly VatReportRepository _repository;
        private readonly OperationalExportBuilder _exportBuilder;
        private readonly ExportDialogService _exportDialog;
        private readonly ExportAuthorizationService _authorization;

        [ObservableProperty] private DateTime _startDate = new(DateTime.Today.Year, DateTime.Today.Month, 1);
        [ObservableProperty] private DateTime _endDate = DateTime.Today;
        [ObservableProperty] private VatReportSummaryDto _summary = new();
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _statusMessage = "Select a date range and generate the report.";

        public ObservableCollection<VatReportDocumentRowDto> SalesAndReturns { get; } = new();
        public ObservableCollection<VatReportDocumentRowDto> PurchasesAndReturns { get; } = new();
        public ObservableCollection<VatCategoryRateRowDto> CategoryRateRows { get; } = new();
        public ObservableCollection<VatLegacyUnknownRowDto> LegacyUnknownRows { get; } = new();
        public ObservableCollection<VatReconciliationRowDto> ReconciliationRows { get; } = new();

        public VatReportViewModel(
            VatReportRepository repository,
            OperationalExportBuilder exportBuilder,
            ExportDialogService exportDialog,
            ExportAuthorizationService authorization)
        {
            _repository = repository;
            _exportBuilder = exportBuilder;
            _exportDialog = exportDialog;
            _authorization = authorization;
            _ = GenerateReportAsync();
        }

        partial void OnIsBusyChanged(bool value)
        {
            ExportPdfCommand.NotifyCanExecuteChanged();
            ExportCsvCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private async Task GenerateReportAsync()
        {
            if (StartDate.Date > EndDate.Date)
            {
                MessageBox.Show("Start Date cannot be later than End Date.", "VAT Report Filter", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = "Reading immutable VAT snapshots...";

                VatReportResultDto result = await _repository.GetReportAsync(StartDate, EndDate);
                Summary = result.Summary;
                Replace(SalesAndReturns, result.Documents.Where(d => d.SourceType == "Sale" || d.SourceType == "Customer Credit Note"));
                Replace(PurchasesAndReturns, result.Documents.Where(d => d.SourceType == "GRN Purchase" || d.SourceType == "Supplier Debit Note"));
                Replace(CategoryRateRows, result.CategoryRateRows);
                Replace(LegacyUnknownRows, result.LegacyUnknownRows);
                Replace(ReconciliationRows, result.ReconciliationRows);

                StatusMessage =
                    $"Report generated for {result.StartDate:yyyy-MM-dd} to {result.EndDate:yyyy-MM-dd}. " +
                    $"{Summary.CompleteDocumentCount} complete documents, " +
                    $"{Summary.LegacyUnknownDocumentCount} unknown items, " +
                    $"{Summary.DiscrepancyCount} discrepancies.";
            }
            catch (Exception ex)
            {
                StatusMessage = "VAT report generation failed.";
                MessageBox.Show($"VAT reports could not be generated.\n\n{ex.Message}", "VAT Report Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                ExportPdfCommand.NotifyCanExecuteChanged();
                ExportCsvCommand.NotifyCanExecuteChanged();
            }
        }

        [RelayCommand(CanExecute = nameof(CanExport))]
        private void ExportPdf()
        {
            try
            {
                _authorization.EnsureBulkFinancialExportAllowed();
                PdfTableDocumentDto document = _exportBuilder.BuildVatSummary(
                    Summary, CategoryRateRows.ToList(), StartDate, EndDate, _authorization.CurrentUsername);
                _exportDialog.SaveTablePdf(
                    "Save VAT Summary PDF",
                    ExportFileNameHelper.Build($"VAT_Summary_{StartDate:yyyyMMdd}_{EndDate:yyyyMMdd}", ".pdf"),
                    document);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "VAT Summary PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        [RelayCommand(CanExecute = nameof(CanExport))]
        private async Task ExportCsvAsync()
        {
            try
            {
                _authorization.EnsureBulkFinancialExportAllowed();
                string csv = _exportBuilder.BuildVatDetailsCsv(
                    SalesAndReturns.Concat(PurchasesAndReturns).ToList(),
                    ReconciliationRows.ToList());
                await _exportDialog.SaveCsvAsync(
                    "Save VAT Detail CSV",
                    ExportFileNameHelper.Build($"VAT_Details_{StartDate:yyyyMMdd}_{EndDate:yyyyMMdd}", ".csv"),
                    csv);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "VAT Detail CSV", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private bool CanExport() => !IsBusy && (Summary.CompleteDocumentCount > 0 || ReconciliationRows.Count > 0 || CategoryRateRows.Count > 0);

        [RelayCommand]
        private async Task ResetFiltersAsync()
        {
            StartDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            EndDate = DateTime.Today;
            await GenerateReportAsync();
        }

        private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
        {
            target.Clear();
            foreach (T value in values)
                target.Add(value);
        }
    }
}
