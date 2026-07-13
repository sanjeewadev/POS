using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class SuspendedTransactionsMonitorViewModel : ViewModelBase
    {
        private readonly CashierCartRepository _repository;

        [ObservableProperty] private DateTime _startDate = DateTime.Today.AddDays(-7);
        [ObservableProperty] private DateTime _endDate = DateTime.Today;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _selectedStatus = "All";
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _statusMessage = "Ready.";
        [ObservableProperty] private BackOfficeCartSessionDto? _selectedSession;
        [ObservableProperty] private BackOfficeCartDetailsDto? _selectedDetails;

        public ObservableCollection<string> Statuses { get; } = new()
        {
            "All", "Active", "Held", "Completed", "Cancelled"
        };

        public ObservableCollection<BackOfficeCartSessionDto> Sessions { get; } = new();
        public bool IsEmpty => !IsBusy && Sessions.Count == 0;

        public SuspendedTransactionsMonitorViewModel(CashierCartRepository repository)
        {
            _repository = repository;
            _ = LoadAsync();
        }

        partial void OnSelectedSessionChanged(BackOfficeCartSessionDto? value)
        {
            _ = LoadDetailsAsync(value?.Id);
        }

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (StartDate.Date > EndDate.Date)
            {
                MessageBox.Show("Start date cannot be later than end date.", "Suspended Transactions", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            StatusMessage = "Loading persistent carts...";
            try
            {
                var rows = await _repository.GetBackOfficeSessionsAsync(StartDate, EndDate, SelectedStatus, SearchText);
                Sessions.Clear();
                foreach (var row in rows)
                    Sessions.Add(row);

                SelectedSession = Sessions.Count > 0 ? Sessions[0] : null;
                StatusMessage = $"{Sessions.Count:N0} cart session(s).";
            }
            catch (Exception ex)
            {
                StatusMessage = "Persistent cart history could not be loaded.";
                MessageBox.Show(ex.Message, "Suspended Transactions", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(IsEmpty));
            }
        }

        private async Task LoadDetailsAsync(int? sessionId)
        {
            if (!sessionId.HasValue)
            {
                SelectedDetails = null;
                return;
            }

            try
            {
                SelectedDetails = await _repository.GetBackOfficeSessionDetailsAsync(sessionId.Value);
            }
            catch (Exception ex)
            {
                SelectedDetails = null;
                MessageBox.Show(ex.Message, "Cart Details", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ResetAsync()
        {
            StartDate = DateTime.Today.AddDays(-7);
            EndDate = DateTime.Today;
            SearchText = string.Empty;
            SelectedStatus = "All";
            await LoadAsync();
        }
    }
}
