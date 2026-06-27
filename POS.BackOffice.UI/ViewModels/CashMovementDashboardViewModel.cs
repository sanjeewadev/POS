using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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

        public ObservableCollection<CashMovement> MovementsList { get; set; } = new();

        public CashMovementDashboardViewModel(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
            _ = LoadDataAsync();
        }

        [RelayCommand]
        private async Task ApplyFiltersAsync() => await LoadDataAsync();

        [RelayCommand]
        private void Export()
        {
            System.Windows.MessageBox.Show("Export to CSV initializing...");
        }

        private async Task LoadDataAsync()
        {
            // ... (keep the loading logic I provided in the previous step) ...
        }
    }
}