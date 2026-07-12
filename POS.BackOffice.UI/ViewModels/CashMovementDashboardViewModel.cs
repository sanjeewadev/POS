using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using POS.Core.Data;
using POS.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class CashMovementDashboardViewModel : ObservableObject
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        [ObservableProperty] private DateTime _startDate = DateTime.Today;
        [ObservableProperty] private DateTime _endDate = DateTime.Today;
        [ObservableProperty] private string _selectedTypeFilter = "All Types";
        [ObservableProperty] private string _searchKeyword = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(NetMovement))]
        private decimal _totalPaidIn;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(NetMovement))]
        private decimal _totalPaidOut;

        public decimal NetMovement => TotalPaidIn - TotalPaidOut;
        public ObservableCollection<CashMovement> MovementsList { get; } = new();

        public CashMovementDashboardViewModel(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
            _ = LoadDataAsync();
        }

        [RelayCommand]
        private Task ApplyFiltersAsync() => LoadDataAsync();

        [RelayCommand]
        private void Export()
        {
            if (MovementsList.Count == 0)
            {
                MessageBox.Show("There are no cash movements to export.", "CSV Export", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Export Cash Movements",
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"Cash_Movements_{StartDate:yyyyMMdd}_{EndDate:yyyyMMdd}.csv",
                AddExtension = true,
                DefaultExt = ".csv"
            };

            if (dialog.ShowDialog() != true)
                return;

            var csv = new StringBuilder();
            csv.AppendLine("DateTime,VoucherNo,ShiftId,Terminal,Type,Category,Amount,Cashier,AuthorizedBy,Remarks");
            foreach (CashMovement movement in MovementsList)
            {
                csv.AppendLine(string.Join(",", new[]
                {
                    Csv(movement.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                    Csv(movement.ReferenceVoucherNo),
                    movement.ShiftSessionId.ToString(CultureInfo.InvariantCulture),
                    Csv(movement.ShiftSession?.TerminalNo ?? string.Empty),
                    Csv(movement.MovementType),
                    Csv(movement.ReasonCategory),
                    movement.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                    Csv(movement.CashierName),
                    Csv(movement.AuthorizedBy),
                    Csv(movement.Remarks)
                }));
            }

            File.WriteAllText(dialog.FileName, csv.ToString(), new UTF8Encoding(true));
            MessageBox.Show("Cash movement CSV exported successfully.", "CSV Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task LoadDataAsync()
        {
            if (StartDate.Date > EndDate.Date)
            {
                MessageBox.Show("Start Date cannot be later than End Date.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DateTime start = StartDate.Date;
            DateTime endExclusive = EndDate.Date.AddDays(1);
            string search = (SearchKeyword ?? string.Empty).Trim();

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            IQueryable<CashMovement> query = context.CashMovements
                .AsNoTracking()
                .Include(movement => movement.ShiftSession)
                .Where(movement => movement.Timestamp >= start && movement.Timestamp < endExclusive);

            if (!string.Equals(SelectedTypeFilter, "All Types", StringComparison.OrdinalIgnoreCase))
                query = query.Where(movement => movement.MovementType == SelectedTypeFilter);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(movement =>
                    movement.ReferenceVoucherNo.Contains(search) ||
                    movement.ReasonCategory.Contains(search) ||
                    movement.Remarks.Contains(search) ||
                    movement.CashierName.Contains(search) ||
                    movement.AuthorizedBy.Contains(search) ||
                    (movement.ShiftSession != null && movement.ShiftSession.TerminalNo.Contains(search)));
            }

            var rows = await query
                .OrderByDescending(movement => movement.Timestamp)
                .ThenByDescending(movement => movement.Id)
                .ToListAsync();

            MovementsList.Clear();
            foreach (CashMovement row in rows)
                MovementsList.Add(row);

            TotalPaidIn = rows
                .Where(row => row.MovementType == "Paid In")
                .Sum(row => row.Amount);
            TotalPaidOut = rows
                .Where(row => row.MovementType == "Paid Out")
                .Sum(row => row.Amount);
        }

        private static string Csv(string? value)
        {
            string safe = value ?? string.Empty;
            return "\"" + safe.Replace("\"", "\"\"") + "\"";
        }
    }
}
