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
    public partial class SecurityAuditViewModel : ViewModelBase
    {
        private readonly SecurityAuditRepository _repository;

        [ObservableProperty] private DateTime _startDate = DateTime.Today.AddDays(-7);
        [ObservableProperty] private DateTime _endDate = DateTime.Today;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _statusMessage = "Ready.";
        [ObservableProperty] private int _totalEventCount;
        [ObservableProperty] private int _failedLoginCount;
        [ObservableProperty] private int _cancelledCartCount;
        [ObservableProperty] private int _customerReturnCount;
        [ObservableProperty] private int _approvalEventCount;
        [ObservableProperty] private int _drawerFailureCount;
        [ObservableProperty] private int _unusualCashierCount;

        public ObservableCollection<SecurityAuditEventDto> Events { get; } = new();
        public ObservableCollection<CashierActivitySummaryDto> CashierActivity { get; } = new();
        public bool IsEmpty => !IsBusy && Events.Count == 0;

        public SecurityAuditViewModel(SecurityAuditRepository repository)
        {
            _repository = repository;
            _ = LoadAsync();
        }

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (StartDate.Date > EndDate.Date)
            {
                MessageBox.Show("Start date cannot be later than end date.", "Security Audit", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            StatusMessage = "Loading security and operational audit events...";
            try
            {
                SecurityAuditResultDto result = await _repository.GetAuditAsync(StartDate, EndDate, SearchText);
                Events.Clear();
                CashierActivity.Clear();
                foreach (var row in result.Events)
                    Events.Add(row);
                foreach (var row in result.CashierActivity)
                    CashierActivity.Add(row);

                TotalEventCount = result.Summary.TotalEventCount;
                FailedLoginCount = result.Summary.FailedLoginCount;
                CancelledCartCount = result.Summary.CancelledCartCount;
                CustomerReturnCount = result.Summary.CustomerReturnCount;
                ApprovalEventCount = result.Summary.ApprovalEventCount;
                DrawerFailureCount = result.Summary.DrawerFailureCount;
                UnusualCashierCount = result.Summary.UnusualCashierCount;
                StatusMessage = $"{Events.Count:N0} audit event(s).";
            }
            catch (Exception ex)
            {
                StatusMessage = "Security Audit could not be loaded.";
                MessageBox.Show(ex.Message, "Security Audit", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(IsEmpty));
            }
        }

        [RelayCommand]
        private async Task ResetAsync()
        {
            StartDate = DateTime.Today.AddDays(-7);
            EndDate = DateTime.Today;
            SearchText = string.Empty;
            await LoadAsync();
        }
    }
}
