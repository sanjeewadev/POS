using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace POS.BackOffice.UI.ViewModels
{
    public class CashMovementDashboardViewModel : INotifyPropertyChanged
    {
        private readonly AppDbContext _context;

        private DateTime _startDate = DateTime.Today;
        public DateTime StartDate
        {
            get => _startDate;
            set { _startDate = value; OnPropertyChanged(); }
        }

        private DateTime _endDate = DateTime.Today;
        public DateTime EndDate
        {
            get => _endDate;
            set { _endDate = value; OnPropertyChanged(); }
        }

        private string _selectedTypeFilter = "All Types";
        public string SelectedTypeFilter
        {
            get => _selectedTypeFilter;
            set { _selectedTypeFilter = value; OnPropertyChanged(); }
        }

        private string _searchKeyword = string.Empty;
        public string SearchKeyword
        {
            get => _searchKeyword;
            set { _searchKeyword = value; OnPropertyChanged(); }
        }

        private decimal _totalPaidIn;
        public decimal TotalPaidIn
        {
            get => _totalPaidIn;
            set { _totalPaidIn = value; OnPropertyChanged(); OnPropertyChanged(nameof(NetMovement)); }
        }

        private decimal _totalPaidOut;
        public decimal TotalPaidOut
        {
            get => _totalPaidOut;
            set { _totalPaidOut = value; OnPropertyChanged(); OnPropertyChanged(nameof(NetMovement)); }
        }

        public decimal NetMovement => TotalPaidIn - TotalPaidOut;

        public ObservableCollection<CashMovement> MovementsList { get; set; } = new ObservableCollection<CashMovement>();

        public ICommand ApplyFiltersCommand { get; }
        public ICommand ExportCommand { get; }

        public CashMovementDashboardViewModel(AppDbContext context)
        {
            _context = context;
            ApplyFiltersCommand = new RelayCommand(async _ => await LoadDataAsync());
            ExportCommand = new RelayCommand(_ => ExportToCsv());

            // Load data automatically when the dashboard opens
            _ = LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            var endOfDay = EndDate.Date.AddDays(1).AddTicks(-1);

            var query = _context.CashMovements
                .Where(m => m.Timestamp >= StartDate.Date && m.Timestamp <= endOfDay)
                .AsQueryable();

            if (SelectedTypeFilter != "All Types")
            {
                query = query.Where(m => m.MovementType == SelectedTypeFilter);
            }

            if (!string.IsNullOrWhiteSpace(SearchKeyword))
            {
                string lowerKeyword = SearchKeyword.ToLower();
                query = query.Where(m =>
                    m.ReferenceVoucherNo.ToLower().Contains(lowerKeyword) ||
                    m.Remarks.ToLower().Contains(lowerKeyword) ||
                    m.ReasonCategory.ToLower().Contains(lowerKeyword) ||
                    m.CashierName.ToLower().Contains(lowerKeyword));
            }

            var results = await query.OrderByDescending(m => m.Timestamp).ToListAsync();

            MovementsList.Clear();
            foreach (var item in results)
            {
                MovementsList.Add(item);
            }

            TotalPaidIn = results.Where(m => m.MovementType == "Paid In").Sum(m => m.Amount);
            TotalPaidOut = results.Where(m => m.MovementType == "Paid Out").Sum(m => m.Amount);
        }

        private void ExportToCsv()
        {
            // Placeholder for future CSV export logic
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null!)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        public RelayCommand(Action<object?> execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute(parameter);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}