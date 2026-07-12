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
using POS.Core.DTOs;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class FloatCashLogViewModel : ViewModelBase
    {
        private readonly FloatCashRepository _repository;

        [ObservableProperty] private DateTime _startDate = DateTime.Today;
        [ObservableProperty] private DateTime _endDate = DateTime.Today;
        [ObservableProperty] private string _searchText = string.Empty;

        [ObservableProperty] private decimal _totalExpectedCash;
        [ObservableProperty] private decimal _totalActualCash;
        [ObservableProperty] private decimal _netVariance;
        [ObservableProperty] private int _discrepancyCount;

        public ObservableCollection<ShiftAuditDto> Shifts { get; } = new();

        [ObservableProperty] private ShiftAuditDto? _selectedShift;
        [ObservableProperty] private bool _isDrillDownOpen;
        public ObservableCollection<CashMovementDto> ShiftMovements { get; } = new();

        public FloatCashLogViewModel(FloatCashRepository repository)
        {
            _repository = repository;
            _ = GenerateAuditAsync();
        }

        [RelayCommand]
        private async Task GenerateAuditAsync()
        {
            if (StartDate > EndDate)
            {
                MessageBox.Show("Start Date cannot be later than End Date.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var rawData = await _repository.GetShiftAuditsAsync(StartDate, EndDate);
                string search = (SearchText ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(search))
                {
                    rawData = rawData.Where(x =>
                        x.CashierName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        x.TerminalNo.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        x.ZReportNo.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                Shifts.Clear();
                foreach (ShiftAuditDto item in rawData)
                    Shifts.Add(item);

                TotalExpectedCash = rawData.Sum(x => x.ExpectedCash);
                TotalActualCash = rawData.Where(x => x.CloseTime.HasValue).Sum(x => x.ActualCash);
                NetVariance = rawData.Where(x => x.CloseTime.HasValue).Sum(x => x.Variance);
                DiscrepancyCount = rawData.Count(x => x.HasDiscrepancy);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Audit Engine Error: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ClearFilters()
        {
            StartDate = DateTime.Today;
            EndDate = DateTime.Today;
            SearchText = string.Empty;
            _ = GenerateAuditAsync();
        }

        [RelayCommand]
        private async Task OpenDrillDownAsync(ShiftAuditDto clickedShift)
        {
            if (clickedShift == null)
                return;

            SelectedShift = clickedShift;
            IsDrillDownOpen = true;
            ShiftMovements.Clear();
            foreach (CashMovementDto record in await _repository.GetShiftLedgerAsync(clickedShift.ShiftId))
                ShiftMovements.Add(record);
        }

        [RelayCommand]
        private void CloseDrillDown() => IsDrillDownOpen = false;

        [RelayCommand]
        private void ExportToExcel()
        {
            if (Shifts.Count == 0)
            {
                MessageBox.Show("There are no shift rows to export.", "CSV Export", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Export Shift Cash Audit",
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"Shift_Cash_Audit_{StartDate:yyyyMMdd}_{EndDate:yyyyMMdd}.csv",
                AddExtension = true,
                DefaultExt = ".csv"
            };

            if (dialog.ShowDialog() != true)
                return;

            var csv = new StringBuilder();
            csv.AppendLine("ShiftId,ZReportNo,Terminal,Cashier,OpenedAt,ClosedAt,Status,OpeningCash,CashTender,CardTender,ChequeTender,OtherTender,PaidInAndFloatIn,PaidOutAndRefunds,ExpectedCash,CountedCash,Variance,AuthorizedBy");
            foreach (ShiftAuditDto row in Shifts)
            {
                csv.AppendLine(string.Join(",", new[]
                {
                    row.ShiftId.ToString(CultureInfo.InvariantCulture),
                    Csv(row.ZReportNo),
                    Csv(row.TerminalNo),
                    Csv(row.CashierName),
                    Csv(row.OpenTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                    Csv(row.CloseTime?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty),
                    Csv(row.Status),
                    Money(row.OpeningFloat),
                    Money(row.CashTenderTotal),
                    Money(row.CardTenderTotal),
                    Money(row.ChequeTenderTotal),
                    Money(row.OtherTenderTotal),
                    Money(row.TotalCashIn - row.CashTenderTotal),
                    Money(row.TotalCashOut),
                    Money(row.ExpectedCash),
                    Money(row.ActualCash),
                    Money(row.Variance),
                    Csv(row.AuthorizedBy)
                }));
            }

            File.WriteAllText(dialog.FileName, csv.ToString(), new UTF8Encoding(true));
            MessageBox.Show("Shift cash audit CSV exported successfully.", "CSV Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
        private static string Csv(string? value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
    }
}
