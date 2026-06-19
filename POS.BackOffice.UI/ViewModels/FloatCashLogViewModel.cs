using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.DTOs;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class FloatCashLogViewModel : ViewModelBase
    {
        private readonly FloatCashRepository _repository;

        // ==========================================
        // 1. FILTER PARAMETERS
        // ==========================================
        [ObservableProperty] private DateTime _startDate = DateTime.Today;
        [ObservableProperty] private DateTime _endDate = DateTime.Today;
        [ObservableProperty] private string _searchText = string.Empty;

        // ==========================================
        // 2. MACRO KPI CARDS
        // ==========================================
        [ObservableProperty] private decimal _totalExpectedCash;
        [ObservableProperty] private decimal _totalActualCash;
        [ObservableProperty] private decimal _netVariance;
        [ObservableProperty] private int _discrepancyCount;

        // ==========================================
        // 3. COLLECTIONS
        // ==========================================
        public ObservableCollection<ShiftAuditDto> Shifts { get; set; } = new();

        // --- DRILL-DOWN PROPERTIES ---
        [ObservableProperty] private ShiftAuditDto? _selectedShift;
        [ObservableProperty] private bool _isDrillDownOpen = false;
        public ObservableCollection<CashMovementDto> ShiftMovements { get; set; } = new();

        public FloatCashLogViewModel(FloatCashRepository repository)
        {
            _repository = repository;
            _ = GenerateAuditAsync();
        }

        // ==========================================
        // 4. THE EXECUTION ENGINE
        // ==========================================
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

                // Apply Local UI Search (Cashier or Terminal)
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    var lowerSearch = SearchText.ToLower();
                    rawData = rawData.Where(x =>
                        x.CashierName.ToLower().Contains(lowerSearch) ||
                        x.TerminalNo.ToLower().Contains(lowerSearch)).ToList();
                }

                // Push to UI
                Shifts.Clear();
                foreach (var item in rawData)
                {
                    Shifts.Add(item);
                }

                // Update KPIs
                TotalExpectedCash = rawData.Sum(x => x.ExpectedCash);
                TotalActualCash = rawData.Sum(x => x.ActualCash);
                NetVariance = rawData.Sum(x => x.Variance);
                DiscrepancyCount = rawData.Count(x => Math.Abs(x.Variance) > 0);
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

        // ==========================================
        // 5. DRILL-DOWN COMMANDS
        // ==========================================
        [RelayCommand]
        private async Task OpenDrillDownAsync(ShiftAuditDto clickedShift)
        {
            if (clickedShift == null) return;

            SelectedShift = clickedShift;
            IsDrillDownOpen = true;

            ShiftMovements.Clear();
            var historyData = await _repository.GetShiftLedgerAsync(clickedShift.ShiftId);
            foreach (var record in historyData)
            {
                ShiftMovements.Add(record);
            }
        }

        [RelayCommand]
        private void CloseDrillDown() => IsDrillDownOpen = false;

        [RelayCommand]
        private void ExportToExcel()
        {
            MessageBox.Show("Audit Export initializing...", "Export to Excel", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}